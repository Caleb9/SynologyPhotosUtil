module private SynologyPhotosAlbumList.ListAlbumCommand

open System
open System.Text

type private FolderInfo =
    { FolderDto: PhotosApi.FolderDto
      Shared: bool }

let private listFolders sendAsync address sid (photoDtos: PhotosApi.PhotoDto seq) =
    let findFolderPathById folderId : Async<int * Result<FolderInfo, ErrorResult>> =
        let getFolderFromApiResult apiQueryingFunction isSharedSpace =
            async {
                let! apiResponseFolderDtoResult =
                    apiQueryingFunction sendAsync address sid (PhotosApi.Folder.Id folderId)

                return
                    apiResponseFolderDtoResult
                    |> Result.map CommandFunctions.extractFolderDtoFromApiResponse
                    |> Result.map (fun folderDto ->
                        { FolderDto = folderDto
                          Shared = isSharedSpace })
            }

        async {
            let! folderResult =
                async {
                    let! personalSpaceFolderResult =
                        getFolderFromApiResult CommandFunctions.getPersonalSpaceApiResponseFolderDtoResult false

                    match personalSpaceFolderResult with
                    | Error (ErrorResult.InvalidApiResponse (_, code)) when
                        PhotosApi.inaccessibleFolderErrorCodes |> Seq.contains code
                        ->
                        return! getFolderFromApiResult CommandFunctions.getSharedSpaceApiResponseFolderDtoResult true
                    | successOrAnotherError -> return successOrAnotherError
                }

            return folderId, folderResult
        }

    let maxDegreeOfParallelism = 8

    async {
        let! folderResults =
            photoDtos
            |> Seq.map (fun p -> p.Folder_id)
            |> Seq.distinct
            |> Seq.map findFolderPathById
            |> fun x -> Async.Parallel(x, maxDegreeOfParallelism)

        let findFolderResult ({ Folder_id = folderId }: PhotosApi.PhotoDto) =
            snd <| Seq.find (fun (id, _) -> id = folderId) folderResults

        return photoDtos |> Seq.map (fun photoDto -> photoDto, findFolderResult photoDto)
    }

let internal invoke
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (albumName: Arguments.AlbumName)
    (sid: SynologyApi.SessionId)
    : Async<Result<string, ErrorResult>> =

    let resultAndThenPhotoNamePrecedence
        (
            { Filename = photo }: PhotosApi.PhotoDto,
            folderResult: Result<FolderInfo, ErrorResult>
        ) =
        match folderResult with
        | Ok { FolderDto = { Name = folder } } -> $"0{folder}{photo}"
        | Error _ -> $"1{photo}"

    let buildOutput
        (stringBuilder: StringBuilder)
        ({ Filename = photoName }: PhotosApi.PhotoDto, folderResult: Result<FolderInfo, ErrorResult>)
        =
        let printToStringBuilder line =
            Printf.bprintf stringBuilder $"{line}{Environment.NewLine}"
            stringBuilder

        printToStringBuilder
        <| match folderResult with
           | Ok { FolderDto = { Name = name }
                  Shared = shared } ->
               match shared with
               | true -> $"S: %s{name}/%s{photoName}"
               | false -> $"P: %s{name}/%s{photoName}"
           | Error (InvalidApiResponse (_, code)) when Seq.contains code PhotosApi.inaccessibleFolderErrorCodes ->
               $"ERROR: %s{photoName} folder inaccessible"
           | Error error -> $"ERROR: Fetching folder for %s{photoName} resulted in %A{error}"

    CommandFunctions.searchForAlbum sendAsync address albumName sid
    |> Result.bindAsyncToAsync (CommandFunctions.listAlbum sendAsync address sid)
    |> Result.mapAsyncToAsync (listFolders sendAsync address sid)
    |> Result.mapAsyncToSync (
        Seq.sortBy resultAndThenPhotoNamePrecedence
        >> Seq.fold buildOutput (StringBuilder())
        >> string
    )
