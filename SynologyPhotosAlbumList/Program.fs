module SynologyPhotosAlbumList.Program

open System.Net.Http
open System.Text
open SynologyPhotosAlbumList

type ErrorOutputAndExitCode = string * int

let listPhotosInAlbum (args: string array) (sendAsync: PhotosApi.SendRequest) : Result<string, ErrorOutputAndExitCode> =

    let selectNamesFromListFolderResult
        (
            { Filename = filename }: PhotosApi.PhotoDto,
            folderResult: Result<PhotosApi.FolderDto, _>
        ) =
        let folderNameResult =
            folderResult
            |> Result.map (fun { Name = folderName } -> folderName)

        filename, folderNameResult

    Arguments.parseArgs args
    |> Result.bindSyncToAsync (fun arguments ->
        PhotosApi.login sendAsync arguments
        |> Result.bindAsyncToAsync (fun loginDto ->
            let sid = loginDto.Sid

            PhotosApi.searchForAlbumPassphrase sendAsync arguments sid
            |> Result.bindAsyncToAsync (PhotosApi.listAlbum sendAsync arguments sid)
            |> Result.mapAsyncToAsync (PhotosApi.listFolders sendAsync arguments sid)))
    |> Result.mapAsyncToSync (List.map selectNamesFromListFolderResult)
    |> Async.RunSynchronously
    |> function
        | Ok photoFolders ->
            let resultAndThenPhotoNamePrecedence (photoName, folderNameResult) =
                match folderNameResult with
                | Ok folder -> $"0{folder}{photoName}"
                | Error photo -> $"1{photo}"

            let buildOutput (outputAccumulator: StringBuilder) (photoName, folderNameResult) =
                let line =
                    match folderNameResult with
                    | Ok folder ->
                        $"%s{folder}/%s{photoName}"
                    | Error (InvalidApiResponse(_, code)) when code = PhotosApi.inaccessibleFolderErrorCode ->
                        $"ERROR: %s{photoName} folder inaccessible"
                    | Error error ->
                        $"ERROR: Fetching folder for %s{photoName} resulted in %A{error}"
                outputAccumulator.AppendLine line

            photoFolders
            |> List.sortBy resultAndThenPhotoNamePrecedence
            |> List.fold buildOutput (StringBuilder())
            |> fun outputBuilder -> Ok <| outputBuilder.ToString()
        | Error errorResult ->
            errorResult
            |> function
                | ErrorResult.InvalidArguments ->
                    sprintf
                        "%s\n%s\n%s"
                        "Supply the following arguments in this order:"
                        "synology_url album_name synology_username password otp_code"
                        "where the otp_code is optional",
                    1
                | ErrorResult.InvalidUrl invalidUrl -> $"{invalidUrl} is not a valid URL", 2
                | ErrorResult.RequestFailed ex -> $"API request failed: {ex.Message}", 3
                | ErrorResult.InvalidHttpResponse (statusCode, reasonPhrase) ->
                    $"Invalid HTTP response {statusCode}: {reasonPhrase}", 4
                | ErrorResult.InvalidApiResponse (requestType, code) -> $"{requestType} failed with API code {code}", 5
                | ErrorResult.AlbumNotFound albumName -> $"Album \"{albumName}\" not found in owned albums", 6
            |> Error

[<EntryPoint>]
let main args =
    use httpClient = new HttpClient()

    let sendRequest request =
        httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)

    match listPhotosInAlbum args sendRequest with
    | Ok output ->
        printfn $"{output}"
        0
    | Error (output, exitCode) ->
        eprintfn $"{output}"
        exitCode
