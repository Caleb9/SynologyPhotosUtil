module SynologyPhotosAlbumList.SynologyApi

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open SynologyPhotosAlbumList

type SendRequest = HttpRequestMessage -> Task<HttpResponseMessage>

type private CreateRequest = unit -> HttpRequestMessage

let sendRequest<'TResponseDto>
    (sendAsync: SendRequest)
    (createRequest: CreateRequest)
    : Async<Result<'TResponseDto, ErrorResult>> =
    let sendRequest' =
        task {
            try
                use request = createRequest ()
                use! response = sendAsync request

                match response.IsSuccessStatusCode with
                | true ->
                    let! dto = HttpContentJsonExtensions.ReadFromJsonAsync<'TResponseDto> response.Content
                    return Ok dto
                | false ->
                    return
                        Error
                        <| ErrorResult.InvalidHttpResponse(response.StatusCode, response.ReasonPhrase)
            with
            | :? HttpRequestException as ex -> return Error <| ErrorResult.RequestFailed ex
        }

    sendRequest' |> Async.AwaitTask

type ApiResponseDto<'TData> =
    { Success: bool
      Error: {| Code: int |} option
      Data: 'TData option }

let validateApiResponseDto
    (requestType: string)
    (dto: ApiResponseDto<'TData>)
    : Result<ApiResponseDto<'TData>, ErrorResult> =
    match dto.Success, dto.Error with
    | true, None -> Ok dto
    | false, Some errorDto ->
        Error
        <| ErrorResult.InvalidApiResponse(requestType, errorDto.Code)
    | _ -> invalidArg (nameof dto) "Unexpected data received"

type QueryParam = string * string

let createRequest
    (baseAddress: Uri)
    (path: string)
    (formUrlEncodedContentKeysAndValues: QueryParam list)
    : HttpRequestMessage =
    let requestUrl =
        let baseAddress, path =
            baseAddress.AbsoluteUri.TrimEnd '/', path.Trim().TrimStart('/')

        Uri $"{baseAddress}/{path}"

    let toKeyValuePair (key, value) = KeyValuePair(key, value)

    new HttpRequestMessage(
        method = HttpMethod.Post,
        requestUri = requestUrl,
        Content = new FormUrlEncodedContent(Seq.map toKeyValuePair formUrlEncodedContentKeysAndValues)
    )

type SessionId = SessionId of string

let createCommonFormUrlEncodedContentKeysAndValues
    (api: string)
    (version: int)
    (method: string)
    : SessionId option -> QueryParam list =
    let queryParams =
        [ ("api", api)
          ("version", $"{version}")
          ("method", method) ]

    function
    | Some (SessionId sid) -> ("_sid", sid) :: queryParams
    | None -> queryParams

let getDataFromResponseDto: ApiResponseDto<'a> -> 'a =
    function
    | { Data = Some data } -> data
    | _ -> invalidArg (nameof ApiResponseDto) "Unexpected data"
