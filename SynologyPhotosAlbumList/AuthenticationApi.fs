module SynologyPhotosAlbumList.AuthenticationApi

open System
open System.Net.Http
open SynologyPhotosAlbumList

type LoginDto = (* cannot be private, otherwise JSON deserializer crashes *) { Sid: string }

let private createLoginRequest
    (baseAddress: Uri)
    (userName: string)
    (password: string)
    (otpCode: string option)
    : HttpRequestMessage =
    let queryParams =
        SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.API.Auth" 7 "login" None
        @ [ ("account", userName)
            ("passwd", password) ]

    let otpCode =
        match otpCode with
        | Some code -> [ ("otp_code", code) ]
        | None -> []

    SynologyApi.createRequest baseAddress "photo/webapi/entry.cgi" (queryParams @ otpCode)

let login
    (sendAsync: SynologyApi.SendRequest)
    ({ Url = url
       Username = username
       Password = password
       OtpCode = otpCode }: Arguments.T)
    : Async<Result<SynologyApi.SessionId, ErrorResult>> =
    let sendLoginRequest =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<LoginDto>> sendAsync
        <| fun () -> createLoginRequest url username password otpCode

    let validateLoginDto =
        SynologyApi.validateApiResponseDto "Login"

    sendLoginRequest
    |> Result.bindAsyncToSync validateLoginDto
    |> Result.mapAsyncToSync SynologyApi.getDataFromResponseDto
    |> Result.mapAsyncToSync (fun { Sid = sid } -> SynologyApi.SessionId sid)
