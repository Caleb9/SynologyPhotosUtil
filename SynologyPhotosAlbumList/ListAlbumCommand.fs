module SynologyPhotosAlbumList.ListAlbumCommand

open System
open System.Text
open FSharp.Control
open SynologyPhotosAlbumList

module private AsyncSeq =
    let head (source: AsyncSeq<'T>) : Async<'T> =
        async {
            match! AsyncSeq.tryFirst source with
            | Some t -> return t
            | None -> return invalidArg (nameof source) "The input sequence was empty."
        }

let private searchForAlbumId sendAsync address (Arguments.AlbumName albumName) sid =
    let getOwnedAlbumsBatchApiResponseDtoResult offset =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.AlbumListDto>> sendAsync
        <| fun () -> PhotosApi.createGetOwnedAlbumsRequest address sid offset

    let validateAlbumsDto =
        SynologyApi.validateApiResponseDto "Getting album id"

    let getOwnedAlbumsBatchResult batchIndex =
        getOwnedAlbumsBatchApiResponseDtoResult (batchIndex * PhotosApi.dataListBatchSize)
        |> Result.bindAsyncToSync validateAlbumsDto
        |> Result.mapAsyncToSync PhotosApi.extractDataListFromResponseDto

    let isSearchedAlbum ({ Name = name }: PhotosApi.AlbumDto) = name = albumName

    let batchContainsAlbum =
        Seq.exists isSearchedAlbum

    let isOkAndAlbumNotFoundAndNotLastBatch asyncResult =
        match asyncResult with
        | Error _ -> false
        | Ok albumsBatch ->
            not
            <| (batchContainsAlbum albumsBatch
                || PhotosApi.isLastBatch albumsBatch)

    let tryFindAlbum =
        Seq.tryFind isSearchedAlbum

    let extractIdOrError albumsBatch =
        match tryFindAlbum albumsBatch with
        | Some album -> Ok album.Id
        | None -> Error <| ErrorResult.AlbumNotFound albumName

    AsyncSeq.initInfiniteAsync (fun index -> getOwnedAlbumsBatchResult (int index))
    |> AsyncSeq.skipWhile isOkAndAlbumNotFoundAndNotLastBatch
    |> AsyncSeq.head
    |> Result.bindAsyncToSync extractIdOrError

let private listAlbum sendAsync baseAddress sid albumId : Async<Result<PhotosApi.PhotoDto list, ErrorResult>> =
    let getPhotosBatchApiResponseDtoResult offset =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.PhotoListDto>> sendAsync
        <| fun () -> PhotosApi.createListPhotosBatchRequest baseAddress sid albumId offset

    let validateListPhotosDto =
        SynologyApi.validateApiResponseDto "Fetching album contents"

    let rec listAlbum' offset =
        let getNextBatchUntilDone validListPhotosDto =
            let photosBatch =
                PhotosApi.extractDataListFromResponseDto validListPhotosDto

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

type private ApiResponseFolderDto = SynologyApi.ApiResponseDto<{| Folder: PhotosApi.FolderDto |}>

let private listFolders sendAsync address sid (photoDtos: PhotosApi.PhotoDto seq) =
    let validateGetFolderDto =
        SynologyApi.validateApiResponseDto "Getting folder"

    let sendAndValidateGetFolderRequest api folderId =
        fun () -> PhotosApi.createGetFolderRequest address sid api folderId
        |> SynologyApi.sendRequest<ApiResponseFolderDto> sendAsync
        |> Result.bindAsyncToSync validateGetFolderDto

    let getPrivateSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.Foto.Browse.Folder"

    let getSharedSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.FotoTeam.Browse.Folder"

    let getFolder folderId : Async<int * Result<PhotosApi.FolderDto, ErrorResult>> =
        async {
            let! getFolderResult =
                async {
                    match! getPrivateSpaceFolder folderId with
                    | Error (ErrorResult.InvalidApiResponse (_, code)) when code = PhotosApi.inaccessibleFolderErrorCode ->
                        return! getSharedSpaceFolder folderId
                    | successOrOtherError -> return successOrOtherError
                }

            return
                (folderId,
                 getFolderResult
                 |> Result.map
                     (fun ({ Data = dataResult }: SynologyApi.ApiResponseDto<{| Folder: PhotosApi.FolderDto |}>) ->
                         dataResult.Value.Folder))
        }

    let maxDegreeOfParallelism = 8

    async {
        let! folderResults =
            photoDtos
            |> Seq.map (fun p -> p.Folder_id)
            |> Seq.distinct
            |> Seq.map getFolder
            |> fun x -> Async.Parallel(x, maxDegreeOfParallelism)

        let findFolderResult ({ Folder_id = folderId }: PhotosApi.PhotoDto) =
            snd
            <| Seq.find (fun (id, _) -> id = folderId) folderResults

        return
            photoDtos
            |> Seq.map (fun photoDto -> photoDto, findFolderResult photoDto)
    }

let invoke
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (albumName: Arguments.AlbumName)
    (sid: SynologyApi.SessionId)
    : Async<Result<string, ErrorResult>> =
    let selectNamesFromListFolderResult
        (
            { Filename = filename }: PhotosApi.PhotoDto,
            folderResult: Result<PhotosApi.FolderDto, _>
        ) =
        let folderNameResult =
            folderResult
            |> Result.map (fun { Name = folderName } -> folderName)

        filename, folderNameResult

    let resultAndThenPhotoNamePrecedence (photoName, folderNameResult) =
        match folderNameResult with
        | Ok folder -> $"0{folder}{photoName}"
        | Error photo -> $"1{photo}"

    let buildOutput (stringBuilder: StringBuilder) (photoName, folderNameResult) =
        let printToStringBuilder line =
            Printf.bprintf stringBuilder $"{line}{Environment.NewLine}"
            stringBuilder

        printToStringBuilder
        <| match folderNameResult with
           | Ok folder -> $"%s{folder}/%s{photoName}"
           | Error (InvalidApiResponse (_, code)) when code = PhotosApi.inaccessibleFolderErrorCode ->
               $"ERROR: %s{photoName} folder inaccessible"
           | Error error -> $"ERROR: Fetching folder for %s{photoName} resulted in %A{error}"

    searchForAlbumId sendAsync address albumName sid
    |> Result.bindAsyncToAsync (listAlbum sendAsync address sid)
    |> Result.mapAsyncToAsync (listFolders sendAsync address sid)
    |> Result.mapAsyncToSync (Seq.map selectNamesFromListFolderResult)
    |> Result.mapAsyncToSync (
        Seq.sortBy resultAndThenPhotoNamePrecedence
        >> Seq.fold buildOutput (StringBuilder())
        >> fun outputBuilder -> outputBuilder.ToString()
    )
