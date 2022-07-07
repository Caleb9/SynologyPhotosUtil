module SynologyPhotosAlbumList.Program

open System.Net.Http
open SynologyPhotosAlbumList

type ErrorOutputAndExitCode = string * int

let listPhotosInAlbum
    (args: string array)
    (sendAsync: SynologyApi.SendRequest)
    : Async<Result<string, ErrorOutputAndExitCode>> =

    Arguments.parseArgs args
    |> Result.bindSyncToAsync (fun arguments ->
        AuthenticationApi.login sendAsync arguments
        |> Result.bindAsyncToAsync (ListAlbumCommand.execute sendAsync arguments))
    |> Result.mapErrorAsyncToSync (function
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
        | ErrorResult.AlbumNotFound albumName -> $"Album \"{albumName}\" not found in owned albums", 6)

[<EntryPoint>]
let main args =
    async {
        use httpClient = new HttpClient()

        let sendRequest request =
            httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)

        match! listPhotosInAlbum args sendRequest with
        | Ok output ->
            printfn $"{output}"
            return 0
        | Error (output, exitCode) ->
            eprintfn $"{output}"
            return exitCode
    }
    |> Async.RunSynchronously
