module SynologyPhotosAlbumList.Arguments

open System

type T =
    { Url: Uri
      AlbumName: string
      Username: string
      Password: string
      OtpCode: string option }

let parseArgs (args: string array) : Result<T, ErrorResult> =
    let parseUrl arg =
        match Uri.TryCreate(arg, UriKind.Absolute) with
        | true, url -> Ok url
        | false, _ -> Error <| ErrorResult.InvalidUrl arg

    let createArguments url albumName userName password otpOption =
        parseUrl url
        |> Result.map (fun url ->
            { Url = url
              AlbumName = albumName
              Username = userName
              Password = password
              OtpCode = otpOption })

    match List.ofArray args with
    | urlString :: albumName :: userName :: password :: otp :: _ ->
        createArguments urlString albumName userName password (Some otp)
    | urlString :: albumName :: userName :: password :: _ -> createArguments urlString albumName userName password None
    | _ -> Error <| ErrorResult.InvalidArguments
