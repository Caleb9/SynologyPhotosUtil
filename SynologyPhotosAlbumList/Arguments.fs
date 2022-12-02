module SynologyPhotosAlbumList.Arguments

open System
open System.Text.RegularExpressions
open SynologyPhotosAlbumList

let internal helpMessage (executableName: string) =
    $"""Usage: %s{executableName} [options] [command] [command-arguments] [options]

Global options:
    -h, --help                  Prints this message

Commands:
    list <ALBUM-NAME>           List photos in album
    
Common command options (available for all commands):
    -a, --address <URL>         [REQUIRED] HTTP(S) address of Synology DSM
    -u, --user <USER-NAME>      [REQUIRED] DSM user account name
    -p, --password <PASSWORD>   [REQUIRED] DSM user account password
    -o, --otp <OTP-CODE>        OTP code when 2FA is enabled for user account

Examples:
    * List photos in album

        %s{executableName} list ""My album"" --address http://my.ds.nas --user my_user --password my_password

      With OTP code if my_user account needs two factor authentication
      
        %s{executableName} list ""My album"" --address http://my.ds.nas --user my_user --password my_password --otp 123456

      When DSM uses non-standard port number (not 5000 for HTTP or 5001 for HTTPS)

        %s{executableName} list ""My album"" --address http://my.ds.nas:8000 --user my_user --password my_password
"""

type Account = Account of string
type Password = Password of string
type Otp = Otp of string

type Credentials =
    { Account: Account
      Password: Password
      Otp: Otp option }

type Address = Address of Uri
type AlbumName = AlbumName of string

type ListAlbumArgs =
    { Address: Address
      Credentials: Credentials
      AlbumName: AlbumName }

type Command =
    | ListAlbum of ListAlbumArgs
    | Help

type private IntermediateArguments =
    { Help: unit option
      ListAlbumCommand: string option
      Address: string option
      Account: string option
      Password: string option
      Otp: string option }
    static member empty =
        { Help = None
          ListAlbumCommand = None
          Address = None
          Account = None
          Password = None
          Otp = None }

let private mapArgumentsToCommands =
    let parseUrl urlString =
        match Uri.TryCreate(urlString, UriKind.Absolute) with
        | true, uri -> Ok uri
        | false, _ -> Error <| ErrorResult.InvalidUrl urlString

    let validateUrlSchemeIsHttpOrHttps (url: Uri) =
        match url.Scheme with
        | scheme when scheme = Uri.UriSchemeHttp
            || scheme = Uri.UriSchemeHttps
            ->
            Ok url
        | _ -> Error <| ErrorResult.InvalidUrl url.OriginalString

    let setPort (url: Uri) =
        let isSpecified =
            Regex.IsMatch(url.OriginalString, ":\d+")

        match isSpecified with
        | false ->
            let port =
                match url.Scheme with
                | scheme when scheme = Uri.UriSchemeHttp -> 5000
                | scheme when scheme = Uri.UriSchemeHttps -> 5001
                | _ -> invalidOp "Unreachable code"

            let builder = UriBuilder(url)
            builder.Port <- port
            builder.Uri
        | true -> url

    function
    | { Help = Some () } -> Ok Help
    | { ListAlbumCommand = Some albumName
        Address = Some urlString
        Account = Some account
        Password = Some password
        Otp = otp } ->
        parseUrl urlString
        |> Result.bind validateUrlSchemeIsHttpOrHttps
        |> Result.map setPort
        |> Result.map (fun url ->
            Command.ListAlbum
                { Address = Address url
                  Credentials =
                    { Account = Account account
                      Password = Password password
                      Otp = Option.map Otp otp }
                  AlbumName = AlbumName albumName })
    | _ -> Error ErrorResult.InvalidArguments

let parseArgs (args: string array) : Result<Command, ErrorResult> =
    let rec tokenizeArgs (arguments: IntermediateArguments) args' =
        match args' with
        | [] -> arguments
        | ("--help"
        | "-h") :: _ -> { IntermediateArguments.empty with Help = Some() }
        | "list" :: albumName :: xs -> tokenizeArgs { arguments with ListAlbumCommand = Some albumName } xs
        | ("--address"
        | "-a") :: url :: xs -> tokenizeArgs { arguments with Address = Some url } xs
        | ("--user"
        | "-u") :: user :: xs -> tokenizeArgs { arguments with Account = Some user } xs
        | ("--password"
        | "-p") :: password :: xs -> tokenizeArgs { arguments with Password = Some password } xs
        | ("--otp"
        | "-o") :: otp :: xs -> tokenizeArgs { arguments with Otp = Some otp } xs
        | _ :: xs -> tokenizeArgs arguments xs

    List.ofArray args
    |> tokenizeArgs IntermediateArguments.empty
    |> mapArgumentsToCommands
