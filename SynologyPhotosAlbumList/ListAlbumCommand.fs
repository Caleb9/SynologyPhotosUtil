module SynologyPhotosAlbumList.ListAlbumCommand

open System
open System.Text
open SynologyPhotosAlbumList

let private searchForAlbumId
    (sendAsync: SynologyApi.SendRequest)
    ({ Url = url; AlbumName = albumName }: Arguments.T)
    (sid: SynologyApi.SessionId)
    =
    let getOwnedAlbumsBatchApiResponseDtoResult offset =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.AlbumListDto>> sendAsync
        <| fun () -> PhotosApi.createGetOwnedAlbumsRequest url sid offset

    let validateAlbumsDto =
        SynologyApi.validateApiResponseDto "Getting album id"

    let getOwnedAlbumsBatchResult batchIndex =
        getOwnedAlbumsBatchApiResponseDtoResult (batchIndex * PhotosApi.dataListBatchSize)
        |> Result.bindAsyncToSync validateAlbumsDto
        |> Result.mapAsyncToSync PhotosApi.extractDataListFromResponseDto

    let isSearchedAlbum ({ Name = name }: PhotosApi.AlbumDto) = name = albumName

    let batchContainsAlbum =
        Seq.exists isSearchedAlbum

    let isOkAndAlbumNotFoundAndNotLastBatch =
        function
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

    Seq.initInfinite getOwnedAlbumsBatchResult
    |> Seq.skipWhile (
        isOkAndAlbumNotFoundAndNotLastBatch
        << Async.RunSynchronously
    )
    |> Seq.head
    |> Result.bindAsyncToSync extractIdOrError

let private listAlbum
    (sendAsync: SynologyApi.SendRequest)
    ({ Url = url }: Arguments.T)
    (sid: SynologyApi.SessionId)
    (albumId: int)
    : Async<Result<PhotosApi.PhotoDto list, ErrorResult>> =
    let getPhotosBatchApiResponseDtoResult offset =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.PhotoListDto>> sendAsync
        <| fun () -> PhotosApi.createListPhotosBatchRequest url sid albumId offset

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

let private listFolders
    (sendAsync: SynologyApi.SendRequest)
    ({ Url = url }: Arguments.T)
    (sid: SynologyApi.SessionId)
    (photoDtos: PhotosApi.PhotoDto seq)
    =
    let validateGetFolderDto =
        SynologyApi.validateApiResponseDto "Getting folder"

    let sendAndValidateGetFolderRequest api folderId =
        fun () -> PhotosApi.createGetFolderRequest url sid api folderId
        |> SynologyApi.sendRequest<ApiResponseFolderDto> sendAsync
        |> Result.bindAsyncToSync validateGetFolderDto

    let getPrivateSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.Foto.Browse.Folder"

    let getSharedSpaceFolder =
        sendAndValidateGetFolderRequest "SYNO.FotoTeam.Browse.Folder"

    let getFolder folderId : Async<int * Result<PhotosApi.FolderDto, ErrorResult>> =
        async {
            let! privateSpaceFolderResult = getPrivateSpaceFolder folderId

            let getFolderResult =
                match privateSpaceFolderResult with
                | Error (ErrorResult.InvalidApiResponse (_, code)) when code = PhotosApi.inaccessibleFolderErrorCode ->
                    getSharedSpaceFolder folderId
                    |> Async.RunSynchronously
                | successOrOtherError -> successOrOtherError

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

let execute
    (sendAsync: SynologyApi.SendRequest)
    (arguments: Arguments.T)
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

    searchForAlbumId sendAsync arguments sid
    |> Result.bindAsyncToAsync (listAlbum sendAsync arguments sid)
    |> Result.mapAsyncToAsync (listFolders sendAsync arguments sid)
    |> Result.mapAsyncToSync (Seq.map selectNamesFromListFolderResult)
    |> Result.mapAsyncToSync (
        Seq.sortBy resultAndThenPhotoNamePrecedence
        >> Seq.fold buildOutput (StringBuilder())
        >> fun outputBuilder -> outputBuilder.ToString()
    )
