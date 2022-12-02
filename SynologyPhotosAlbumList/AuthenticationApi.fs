module SynologyPhotosAlbumList.AuthenticationApi

open SynologyPhotosAlbumList

type public LoginDto = (* cannot be private, otherwise JSON deserializer crashes *) { Sid: string }

let private createLoginRequest
    address
    ({ Account = Arguments.Account account
       Password = Arguments.Password password
       Otp = otp }: Arguments.Credentials)
    =
    let queryParams =
        SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.API.Auth" 7 "login" None
        @ [ ("account", account)
            ("passwd", password) ]

    let otpCode =
        match otp with
        | Some (Arguments.Otp code) -> [ ("otp_code", code) ]
        | None -> []

    SynologyApi.createRequest address "webapi/entry.cgi" (queryParams @ otpCode)

let internal login
    (sendAsync: SynologyApi.SendRequest)
    (address: Arguments.Address)
    (credentials: Arguments.Credentials)
    : Async<Result<SynologyApi.SessionId, ErrorResult>> =
    let sendLoginRequest =
        SynologyApi.sendRequest<SynologyApi.ApiResponseDto<LoginDto>> sendAsync
        <| fun () -> createLoginRequest address credentials

    let validateLoginDto =
        SynologyApi.validateApiResponseDto "Login"

    sendLoginRequest
    |> Result.bindAsyncToSync validateLoginDto
    |> Result.mapAsyncToSync SynologyApi.getDataFromResponseDto
    |> Result.mapAsyncToSync (fun { Sid = sid } -> SynologyApi.SessionId sid)
