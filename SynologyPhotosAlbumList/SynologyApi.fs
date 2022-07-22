module SynologyPhotosAlbumList.SynologyApi

open System
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open SynologyPhotosAlbumList

type internal SendRequest = HttpRequestMessage -> Task<HttpResponseMessage>

type private CreateRequest = unit -> HttpRequestMessage

let internal sendRequest<'TResponseDto>
    (sendAsync: SendRequest)
    (createRequest: CreateRequest)
    : Async<Result<'TResponseDto, ErrorResult>> =
    Async.AwaitTask
    <| task {
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

type public ApiResponseDto<'TData> =
    { Success: bool
      Error: {| Code: int |} option
      Data: 'TData option }

let internal validateApiResponseDto
    (requestType: string)
    (dto: ApiResponseDto<'TData>)
    : Result<ApiResponseDto<'TData>, ErrorResult> =
    match dto.Success, dto.Error with
    | true, None -> Ok dto
    | false, Some errorDto ->
        Error
        <| ErrorResult.InvalidApiResponse(requestType, errorDto.Code)
    | _ -> invalidArg (nameof dto) "Unexpected data received"

type private QueryParam = string * string

let internal createRequest
    (Arguments.Address address)
    (path: string)
    (formUrlEncodedContentKeysAndValues: QueryParam seq)
    : HttpRequestMessage =
    let requestUrl =
        let baseAddress, path =
            address.AbsoluteUri.TrimEnd '/', path.Trim().TrimStart('/')

        Uri $"{baseAddress}/{path}"

    let toKeyValuePair (key, value) = KeyValuePair(key, value)

    new HttpRequestMessage(
        method = HttpMethod.Post,
        requestUri = requestUrl,
        Content = new FormUrlEncodedContent(Seq.map toKeyValuePair formUrlEncodedContentKeysAndValues)
    )

type internal SessionId = SessionId of string

let internal createCommonFormUrlEncodedContentKeysAndValues
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

let internal getDataFromResponseDto: ApiResponseDto<'a> -> 'a =
    function
    | { Data = Some data } -> data
    | _ -> invalidArg (nameof ApiResponseDto) "Unexpected data received"
