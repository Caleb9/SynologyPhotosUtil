module private SynologyPhotosUtil.ExportAlbumCommand

let private findFolderId sendAsync address sid (Arguments.FolderPath path) =
    let sanitizedPath = $"/%s{path.Trim().Trim('/')}"

    async {
        let! folderDtoResponseResult =
            CommandFunctions.getPersonalSpaceApiResponseFolderDtoResult
                sendAsync
                address
                sid
                (PhotosApi.Folder.Path sanitizedPath)

        return
            folderDtoResponseResult
            |> Result.map CommandFunctions.extractFolderDtoFromApiResponse
            |> Result.map (fun folderDto -> folderDto.Id)
            |> Result.mapError (function
                | ErrorResult.InvalidApiResponse (_, PhotosApi.folderNotFoundErrorCode) ->
                    ErrorResult.FolderNotFound sanitizedPath
                | anyOtherError -> anyOtherError)
    }

(* From a sequence of Results, return the first Error or a single result containing sequence of all Ok values *)
let private foldResults (results: seq<Result<'T, ErrorResult>>) : Result<seq<'T>, ErrorResult> =
    Seq.fold
        (fun acc r ->
            match acc, r with
            | Ok xs, Ok x -> Ok <| Seq.append [ x ] xs
            | Ok _, Error e -> Error e
            | _ -> acc)
        (Ok [])
        results

let private copyPhotos sendAsync address sid targetFolderId photoDtos =
    async {
        let personalSpacePhotoDtos, sharedSpacePhotoDtos =
            Seq.fold
                (fun (personal, shared) (dto: PhotosApi.PhotoDto) ->
                    match dto.Owner_user_id with
                    | 0 -> personal, dto :: shared
                    | _ -> dto :: personal, shared)
                ([], [])
                photoDtos

        let createRequest space dtos =
            PhotosApi.createCopyPhotoRequest
                address
                sid
                space
                (Seq.map (fun (dto: PhotosApi.PhotoDto) -> dto.Id) dtos)
                targetFolderId

        let copyPersonalPhotosRequest =
            createRequest PhotosApi.Space.Personal personalSpacePhotoDtos

        let copySharedPhotosRequest =
            createRequest PhotosApi.Space.Shared sharedSpacePhotoDtos

        let sendRequest request =
            SynologyApi.sendRequest<SynologyApi.ApiResponseDto<PhotosApi.TaskInfoDto>> sendAsync (fun () -> request)

        let! taskResults =
            [ sendRequest copyPersonalPhotosRequest; sendRequest copySharedPhotosRequest ]
            |> Async.Parallel

        return foldResults taskResults
    }

let internal invoke
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (albumName: Arguments.AlbumName)
    (personalSpaceFolderPath: Arguments.FolderPath)
    (sid: SynologyApi.SessionId)
    : Async<Result<string, ErrorResult>> =
    async {
        let! asyncPhotosResult =
            CommandFunctions.searchForAlbum sendAsync address albumName sid
            |> Result.bindAsyncToAsync (CommandFunctions.listAlbum sendAsync address sid)
            |> Async.StartChild

        let! asyncTargetFolderIdResult = findFolderId sendAsync address sid personalSpaceFolderPath |> Async.StartChild

        let! photosResult = asyncPhotosResult
        let! targetFolderIdResult = asyncTargetFolderIdResult

        return!
            photosResult
            |> Result.bindSyncToAsync (fun photoDtos ->
                targetFolderIdResult
                |> Result.bindSyncToAsync (fun targetFolderId ->
                    copyPhotos sendAsync address sid targetFolderId photoDtos))
            |> Result.bindAsyncToSync (foldResults << Seq.map (SynologyApi.validateApiResponseDto "Copy photos"))
            |> Result.bindAsyncToSync (fun _ ->
                Ok "Album export started. Please see Background Tasks progress in the Synology Photos web interface.")
    }
