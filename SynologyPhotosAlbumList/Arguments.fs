module SynologyPhotosAlbumList.Arguments

open System

type T =
    { Url: Uri
      AlbumName: string
      Username: string
      Password: string
      OtpCode: string option }

let parseArgs (args: string array) : Result<T, ErrorResult> =
    let hasRequiredArguments =
        Array.length args >= 4

    let hasOptionalOtpCodeArgument =
        Array.length args >= 5

    let otpCode =
        match hasOptionalOtpCodeArgument with
        | true -> Some args[4]
        | false -> None

    match hasRequiredArguments with
    | true ->
        match Uri.TryCreate(args[0], UriKind.Absolute) with
        | true, url ->
            Ok
            <| { Url = url
                 AlbumName = args[1]
                 Username = args[2]
                 Password = args[3]
                 OtpCode = otpCode }
        | _ -> Error <| ErrorResult.InvalidUrl args[0]
    | false -> Error <| ErrorResult.InvalidArguments
