module private SynologyPhotosUtil.CommandFunctions

open FSharp.Control
open SynologyPhotosUtil

let internal searchForAlbum
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (Arguments.AlbumName albumName)
    (sid: SynologyApi.SessionId)
    : Async<Result<PhotosApi.AlbumDto, ErrorResult>> =
    async {
        let! searchAlbumsApiResponseDtoResult =
            SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.AlbumListDto>> sendAsync
            <| fun () -> PhotosApi.createSearchAlbumsRequest address sid albumName

        let searchAlbumsResult =
            searchAlbumsApiResponseDtoResult
            |> Result.bind (SynologyApi.validateApiResponseDto "Getting album id")
            |> Result.map PhotosApi.extractDataListFromResponseDto

        let isSearchedAlbum ({ Name = name }: PhotosApi.AlbumDto) = name = albumName

        let foundAlbumOrError albumsList =
            match Seq.tryFind isSearchedAlbum albumsList with
            | Some album -> Ok album
            | None -> Error <| ErrorResult.AlbumNotFound albumName

        return searchAlbumsResult |> Result.bind foundAlbumOrError
    }

let private listPhotosInAlbum sendAsync baseAddress sid albumId =
    let getPhotosBatchApiResponseDtoResult offset =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.PhotoListDto>> sendAsync
        <| fun () -> PhotosApi.createListPhotosBatchRequest baseAddress sid albumId offset

    let validateListPhotosDto =
        SynologyApi.validateApiResponseDto "Fetching album contents"

    let rec listAlbum' offset =
        let getNextBatchUntilDone validListPhotosDto =
            let photosBatch = PhotosApi.extractDataListFromResponseDto validListPhotosDto

            async {
                match photosBatch with
                | photos when PhotosApi.isLastBatch photos -> return Ok photos
                | photos ->
                    return!
                        listAlbum' (offset + PhotosApi.dataListBatchSize)
                        |> Result.mapAsyncToSync (fun nextBatch -> photos @ nextBatch)
            }

        getPhotosBatchApiResponseDtoResult offset
        |> Result.bindAsyncToSync validateListPhotosDto
        |> Result.bindAsyncToAsync getNextBatchUntilDone

    listAlbum' 0

let internal listAlbum
    (sendAsync: SynologyApi.SendRequest)
    (baseAddress: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (albumDto: PhotosApi.AlbumDto)
    : Async<Result<PhotosApi.PhotoDto list, ErrorResult>> =
    async {
        match albumDto.Type with
        | "album" -> return! listPhotosInAlbum sendAsync baseAddress sid (PhotosApi.Album.Id albumDto.Id)
        | "person" -> return! listPhotosInAlbum sendAsync baseAddress sid (PhotosApi.Album.Person albumDto.Id)
        | "shared_with_me" ->
            return! listPhotosInAlbum sendAsync baseAddress sid (PhotosApi.Album.Passphrase albumDto.Passphrase)
        | _ -> return Error <| ErrorResult.UnexpectedAlbumType albumDto.Type
    }

type internal ApiResponseFolderDto = SynologyApi.ApiResponseDto<{| Folder: PhotosApi.FolderDto |}>

let private sendAndValidateGetFolderRequest sendAsync address sid space folder =
    let validateGetFolderDto = SynologyApi.validateApiResponseDto "Getting folder"

    fun () -> PhotosApi.createGetFolderRequest address sid space folder
    |> SynologyApi.sendRequest<ApiResponseFolderDto> sendAsync
    |> Result.bindAsyncToSync validateGetFolderDto

let internal getPersonalSpaceApiResponseFolderDtoResult
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (folder: PhotosApi.Folder)
    : Async<Result<ApiResponseFolderDto, ErrorResult>> =
    sendAndValidateGetFolderRequest sendAsync address sid PhotosApi.Space.Personal folder

let internal getSharedSpaceApiResponseFolderDtoResult
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (folder: PhotosApi.Folder)
    : Async<Result<ApiResponseFolderDto, ErrorResult>> =
    sendAndValidateGetFolderRequest sendAsync address sid PhotosApi.Space.Shared folder

let internal extractFolderDtoFromApiResponse ({ Data = dataDto }: ApiResponseFolderDto) : PhotosApi.FolderDto =
    match dataDto with
    | Some data -> data.Folder
    | None -> invalidArg (nameof dataDto) "Unexpected data received"
