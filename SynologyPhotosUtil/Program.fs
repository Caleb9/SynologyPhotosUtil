module SynologyPhotosUtil.Program

open System
open System.Net.Http
open Microsoft.Extensions.Logging
open SynologyPhotosUtil

type private ErrorOutputAndExitCode = string * int

let public execute
    (args: string array)
    (executableName: string)
    (sendAsync: SynologyApi.SendRequest)
    (logger: ILogger)
    : Async<Result<string, ErrorOutputAndExitCode>> =

    (* TODO conditionally enable this *)
    let sendAsyncWithLogger request =
        task {
            logger.LogDebug(string request)
            let! response = sendAsync request
            logger.LogDebug(string response)
            return response
        }

    Arguments.parseArgs args
    |> Result.bindSyncToAsync (fun command ->
        match command with
        | Arguments.Command.ListAlbum { Address = address
                                        Credentials = credentials
                                        AlbumName = albumName } ->
            AuthenticationApi.login sendAsync address credentials
            |> Result.bindAsyncToAsync (ListAlbumCommand.invoke sendAsync address albumName)
        | Arguments.Command.ExportAlbum { Address = address
                                          Credentials = credentials
                                          AlbumName = albumName
                                          FolderPath = folderPath } ->
            AuthenticationApi.login sendAsync address credentials
            |> Result.bindAsyncToAsync (ExportAlbumCommand.invoke sendAsync address albumName folderPath)
        | Arguments.Command.Help -> async { return Ok <| Arguments.helpMessage executableName })
    |> Result.mapErrorAsyncToSync (function
        | ErrorResult.InvalidArguments -> "Invalid arguments. Execute with --help option to see available arguments", 1
        | ErrorResult.InvalidUrl invalidUrl -> $"%s{invalidUrl} is not a valid URL", 2
        | ErrorResult.RequestFailed ex -> $"API request failed: %s{ex.Message}", 3
        | ErrorResult.InvalidHttpResponse (statusCode, reasonPhrase) ->
            $"Invalid HTTP response %A{statusCode}: %s{reasonPhrase}", 4
        | ErrorResult.InvalidApiResponse (requestType, code) -> $"%s{requestType} failed with API code {code}", 5
        | ErrorResult.AlbumNotFound albumName -> $"""Album "%s{albumName}" not found in owned albums""", 6
        | ErrorResult.UnexpectedAlbumType albumType -> $"""Unsupported album type {$"%s{albumType}"}""", 7
        | ErrorResult.FolderNotFound folderPath -> $"""Folder "%s{folderPath}" not found in personal space""", 8)

[<EntryPoint>]
let internal main args =
    async {
        let executableName = AppDomain.CurrentDomain.FriendlyName

        use httpClient = new HttpClient()

        let sendRequest request =
            httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)

        use loggerFactory =
            LoggerFactory.Create(fun builder -> builder.SetMinimumLevel(LogLevel.Debug).AddSimpleConsole() |> ignore)

        let logger = loggerFactory.CreateLogger executableName

        match! execute args executableName sendRequest logger with
        | Ok output ->
            printfn $"{output}"
            return 0
        | Error (output, exitCode) ->
            eprintfn $"{output}"
            return exitCode
    }
    |> Async.RunSynchronously
