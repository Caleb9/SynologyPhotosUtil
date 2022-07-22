module SynologyPhotosAlbumList.Program

open System
open System.Net.Http
open SynologyPhotosAlbumList

type ErrorOutputAndExitCode = string * int

let execute
    (args: string array)
    (executableName: string)
    (sendAsync: SynologyApi.SendRequest)
    : Async<Result<string, ErrorOutputAndExitCode>> =

    Arguments.parseArgs args
    |> Result.bindSyncToAsync (fun command ->
        match command with
        | Arguments.Command.ListAlbum { Address=address; Credentials=credentials; AlbumName=albumName } ->
            AuthenticationApi.login sendAsync address credentials
            |> Result.bindAsyncToAsync (ListAlbumCommand.invoke sendAsync address albumName)
        | Arguments.Command.Help -> async { return Ok <| Arguments.helpMessage executableName })
    |> Result.mapErrorAsyncToSync (function
        | ErrorResult.InvalidArguments -> "Invalid arguments. Execute with --help option to see available arguments", 1
        | ErrorResult.InvalidUrl invalidUrl -> $"{invalidUrl} is not a valid URL", 2
        | ErrorResult.RequestFailed ex -> $"API request failed: {ex.Message}", 3
        | ErrorResult.InvalidHttpResponse (statusCode, reasonPhrase) ->
            $"Invalid HTTP response {statusCode}: {reasonPhrase}", 4
        | ErrorResult.InvalidApiResponse (requestType, code) -> $"{requestType} failed with API code {code}", 5
        | ErrorResult.AlbumNotFound albumName -> $"Album \"{albumName}\" not found in owned albums", 6)

[<EntryPoint>]
let main args =
    async {
        let executableName =
            AppDomain.CurrentDomain.FriendlyName

        use httpClient = new HttpClient()

        let sendRequest request =
            httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)

        match! execute args executableName sendRequest with
        | Ok output ->
            printfn $"{output}"
            return 0
        | Error (output, exitCode) ->
            eprintfn $"{output}"
            return exitCode
    }
    |> Async.RunSynchronously
