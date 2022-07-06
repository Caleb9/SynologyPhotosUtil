module SynologyPhotosAlbumList.PhotosApi

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open SynologyPhotosAlbumList

type SendRequest = HttpRequestMessage -> Task<HttpResponseMessage>

type private CreateRequest = unit -> HttpRequestMessage

let private sendRequest<'TResponseDto> (sendAsync: SendRequest) (createRequest: CreateRequest) =
    let sendRequest' =
        task {
            try
                use request = createRequest ()
                use! response = sendAsync request

                match response.IsSuccessStatusCode with
                | true ->
                    let! dto = HttpContentJsonExtensions.ReadFromJsonAsync<'TResponseDto> response.Content
                    return Ok dto
                | false ->
                    return
                        Error
                        <| ErrorResult.InvalidHttpResponse(response.StatusCode, response.ReasonPhrase)
            with
            | :? HttpRequestException as ex -> return Error <| ErrorResult.RequestFailed ex
        }

    sendRequest' |> Async.AwaitTask

type ApiResponseDto<'TData> =
    { Success: bool
      Error: {| Code: int |} option
      Data: 'TData option }

let private validateApiResponseDto requestType (dto: ApiResponseDto<'TData>) =
    match dto.Success, dto.Error with
    | true, None -> Ok dto
    | false, Some errorDto ->
        Error
        <| ErrorResult.InvalidApiResponse(requestType, errorDto.Code)
    | _ -> invalidArg (nameof dto) "Unexpected data received"

let private createRequest (baseAddress: Uri) (path: string) formUrlEncodedContentKeysAndValues =
    let requestUrl =
        let baseAddress, path =
            baseAddress.AbsoluteUri.TrimEnd '/', path.Trim().TrimStart('/')

        Uri $"{baseAddress}/{path}"

    let toKeyValuePair (key, value) = KeyValuePair(key, value)

    new HttpRequestMessage(
        method = HttpMethod.Post,
        requestUri = requestUrl,
        Content = new FormUrlEncodedContent(List.map toKeyValuePair formUrlEncodedContentKeysAndValues)
    )

let private createCommonFormUrlEncodedContentKeysAndValues api version method (sid: string option) =
    let queryParams =
        [ ("api", api)
          ("version", $"{version}")
          ("method", method) ]

    match sid with
    | Some sid -> ("_sid", sid) :: queryParams
    | _ -> queryParams

let private getDataFromResponseDto =
    function
    | { Data = Some data } -> data
    | _ -> invalidArg (nameof ApiResponseDto) "Unexpected data"

type LoginDto = (* cannot be private, otherwise JSON deserializer crashes *) { Sid: string }

let login
    (sendAsync: SendRequest)
    ({ Url = url
       Username = username
       Password = password
       OtpCode = otpCode }: Arguments.T)
    : Async<Result<LoginDto, ErrorResult>> =
    let sendLoginRequest =
        let createLoginRequest () =
            let queryParams =
                createCommonFormUrlEncodedContentKeysAndValues "SYNO.API.Auth" 7 "login" None
                @ [ ("account", username)
                    ("passwd", password) ]

            let otpCode =
                match otpCode with
                | Some code -> [ ("otp_code", code) ]
                | _ -> []

            createRequest url "photo/webapi/entry.cgi" (queryParams @ otpCode)

        sendRequest<ApiResponseDto<LoginDto>> sendAsync createLoginRequest

    let validateLoginDto =
        validateApiResponseDto "Login"

    sendLoginRequest
    |> Result.bindAsyncToSync validateLoginDto
    |> Result.mapAsyncToSync getDataFromResponseDto

type AlbumItemDto =
    { Id: int
      Name: string
      Passphrase: string }

type DataList<'TItem> = { List: 'TItem list }
type AlbumsDtoData = DataList<AlbumItemDto>

let private getDataListFromResponseDto dto =
    let data = getDataFromResponseDto dto
    data.List

let private dataListBatchSize = 100

let private isLastBatch batch = List.length batch < dataListBatchSize

let searchForAlbumPassphrase
    (sendAsync: SendRequest)
    ({ Url = url; AlbumName = albumName }: Arguments.T)
    (sid: string)
    : Async<Result<string, ErrorResult>> =
    let getOwnedAlbumsBatch offset =
        let createGetOwnedAlbumsRequest () =
            createRequest
                url
                "photo/webapi/entry.cgi/SYNO.Foto.Browse.Album"
                (createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Album" 2 "list" (Some sid)
                 @ [ ("offset", $"{offset}")
                     ("limit", $"{dataListBatchSize}")
                     ("sort_by", "album_name")
                     ("sort_direction", "asc") ])

        sendRequest<ApiResponseDto<AlbumsDtoData>> sendAsync createGetOwnedAlbumsRequest

    let validateAlbumsDto =
        validateApiResponseDto "Getting album passphrase"

    let rec searchForAlbumPassphrase' offset =
        let findPassphrase validAlbumsDto =
            let findAlbumInBatch =
                List.tryFind (fun (album: AlbumItemDto) -> album.Name = albumName)

            let albumsBatch =
                getDataListFromResponseDto validAlbumsDto

            async {
                match albumsBatch, findAlbumInBatch albumsBatch with
                | _, Some album -> return Ok <| album.Passphrase
                | albums, None when isLastBatch albums -> return Error <| ErrorResult.AlbumNotFound albumName
                | _ -> return! searchForAlbumPassphrase' (offset + dataListBatchSize)
            }

        let getOwnedAlbumsBatchAndFindPassphrase =
            getOwnedAlbumsBatch
            >> Result.bindAsyncToSync validateAlbumsDto
            >> Result.bindAsyncToAsync findPassphrase

        getOwnedAlbumsBatchAndFindPassphrase offset

    searchForAlbumPassphrase' 0

type PhotoDto =
    { Id: int
      Owner_user_id: int
      Folder_id: int
      Filename: string }

type PhotosDataDto = DataList<PhotoDto>

let listAlbum
    (sendAsync: SendRequest)
    ({ Url = url }: Arguments.T)
    (sid: string)
    (passphrase: string)
    : Async<Result<PhotoDto list, ErrorResult>> =
    let listPhotosBatch offset =
        let createListPhotosBatchRequest () =
            createRequest
                url
                "photo/webapi/entry.cgi"
                (createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Item" 1 "list" (Some sid)
                 @ [ ("passphrase", passphrase)
                     ("offset", $"{offset}")
                     ("limit", $"{dataListBatchSize}")
                     ("sort_by", "filename")
                     ("sort_direction", "asc") ])

        sendRequest<ApiResponseDto<PhotosDataDto>> sendAsync createListPhotosBatchRequest

    let validateListPhotosDto =
        validateApiResponseDto "Fetching album contents"

    let rec listAlbum' offset =
        let getNextBatchUntilDone validListPhotosDto =
            let photosBatch =
                getDataListFromResponseDto validListPhotosDto

            async {
                match photosBatch with
                | photos when isLastBatch photos -> return Ok photos
                | photos ->
                    return!
                        listAlbum' (offset + dataListBatchSize)
                        |> Result.mapAsyncToSync (fun nextBatch -> photos @ nextBatch)
            }

        listPhotosBatch offset
        |> Result.bindAsyncToSync validateListPhotosDto
        |> Result.bindAsyncToAsync getNextBatchUntilDone

    listAlbum' 0

type FolderDto = { Id: int; Name: string }

type PhotoToFolderResultMapping = PhotoDto * Result<FolderDto, ErrorResult>

let inaccessibleFolderErrorCode = 642

let listFolders
    (sendAsync: SendRequest)
    ({ Url = url }: Arguments.T)
    (sid: string)
    (photoDtos: PhotoDto list)
    : Async<PhotoToFolderResultMapping list> =
    let createGetFolderRequest api folderId =
        createRequest
            url
            $"photo/webapi/entry.cgi/{api}"
            (createCommonFormUrlEncodedContentKeysAndValues api 1 "get" (Some sid)
             @ [ ("api", api)
                 ("version", "1")
                 ("id", $"{folderId}") ])

    let validateGetFolderDto =
        validateApiResponseDto "Getting folder"

    let sendAndValidateGetFolderRequest api folderId =
        fun () -> createGetFolderRequest api folderId
        |> sendRequest<ApiResponseDto<{| Folder: FolderDto |}>> sendAsync
        |> Result.bindAsyncToSync validateGetFolderDto

    let getPrivateSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.Foto.Browse.Folder"

    let getSharedSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.FotoTeam.Browse.Folder"

    let getFolder folderId : Async<int * Result<FolderDto, ErrorResult>> =
        async {
            let! privateSpaceFolderResult = getPrivateSpaceFolder folderId

            let getFolderResult =
                match privateSpaceFolderResult with
                | Error (ErrorResult.InvalidApiResponse (_, code)) when code = inaccessibleFolderErrorCode ->
                    getSharedSpaceFolder folderId
                    |> Async.RunSynchronously
                | successOrOtherError -> successOrOtherError

            return
                (folderId,
                 getFolderResult
                 |> Result.map (fun ({ Data = dataResult }: ApiResponseDto<{| Folder: FolderDto |}>) ->
                     dataResult.Value.Folder))
        }

    let maxDegreeOfParallelism = 8

    async {
        let! folderResults =
            photoDtos
            |> List.map (fun p -> p.Folder_id)
            |> List.distinct
            |> List.map getFolder
            |> fun x -> Async.Parallel(x, maxDegreeOfParallelism)

        let findFolderResult { Folder_id = folderId } =
            snd
            <| Array.find (fun (id, _) -> id = folderId) folderResults

        return
            photoDtos
            |> List.map (fun photoDto -> photoDto, findFolderResult photoDto)
    }
