module SynologyPhotosUtil.PhotosApi

open System.Net.Http
open SynologyPhotosUtil

type public AlbumDto =
    { Id: int
      Name: string
      Type: string
      Passphrase: string }

type public ListDto<'TItem> = { List: 'TItem list }
type internal AlbumListDto = ListDto<AlbumDto>

let internal extractDataListFromResponseDto (dto: SynologyApi.ApiResponseDto<ListDto<'a>>) : 'a list =
    let { List = dataList } = SynologyApi.extractDataFromResponseDto dto

    dataList

let internal dataListBatchSize = 100

let internal isLastBatch batch = Seq.length batch < dataListBatchSize

let internal createSearchAlbumsRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (albumName: string)
    : HttpRequestMessage =
    SynologyApi.createRequest address "webapi/entry.cgi/SYNO.Foto.Search.Search"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Search.Search" 4 "suggest" (Some sid)
       @ [ ("keyword", albumName) ]

(* Following could be used for deep album search, in case suggestions don't work, e.g. when album name is very short *)
// let internal createGetOwnedAlbumsRequest
//     (address: Arguments.Address)
//     (sid: SynologyApi.SessionId)
//     (offset: int)
//     : HttpRequestMessage =
//     SynologyApi.createRequest address "webapi/entry.cgi/SYNO.Foto.Browse.Album"
//     <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Album" 2 "list" (Some sid)
//        @ [ ("offset", $"{offset}")
//            ("limit", $"{dataListBatchSize}")
//            ("sort_by", "album_name")
//            ("sort_direction", "asc") ]
//
// let internal createGetSharedWithMeAlbumsRequest
//     (address: Arguments.Address)
//     (sid: SynologyApi.SessionId)
//     (offset: int)
//     : HttpRequestMessage =
//     SynologyApi.createRequest address "webapi/entry.cgi/SYNO.Foto.Sharing.Misc"
//     <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues
//         "SYNO.Foto.Sharing.Misc"
//         2
//         "list_shared_with_me_album"
//         (Some sid)
//        @ [ ("offset", $"{offset}")
//            ("limit", $"{dataListBatchSize}")
//            ("sort_by", "album_name")
//            ("sort_direction", "asc") ]

type public PhotoDto =
    { Id: int
      Owner_user_id: int
      Folder_id: int
      Filename: string }

type internal PhotoListDto = ListDto<PhotoDto>

type internal Album =
    | Id of int
    | Passphrase of string
    | Person of int

let internal createListPhotosBatchRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (album: Album)
    (offset: int)
    : HttpRequestMessage =
    let albumIdParam =
        match album with
        | Id id -> ("album_id", string id)
        | Person id -> ("person_id", string id)
        | Passphrase passphrase -> ("passphrase", passphrase)

    SynologyApi.createRequest address "webapi/entry.cgi"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Item" 1 "list" (Some sid)
       @ [ albumIdParam
           ("offset", string offset)
           ("limit", string dataListBatchSize)
           ("sort_by", "filename")
           ("sort_direction", "asc") ]

type public FolderDto = { Id: int; Name: string }

type internal FolderListDto = ListDto<FolderDto>

[<Literal>]
let internal folderNotFoundErrorCode = 642

let internal inaccessibleFolderErrorCodes = [ folderNotFoundErrorCode; 801; 803 ]

type internal Space =
    | Shared
    | Personal

    member this.Value =
        match this with
        | Shared -> "SYNO.FotoTeam"
        | Personal -> "SYNO.Foto"

type internal Folder =
    | Id of int
    | Path of string

let internal createGetFolderRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (space: Space)
    (folder: Folder)
    : HttpRequestMessage =
    let folderParam =
        match folder with
        | Id id -> ("id", string id)
        | Path path -> ("name", path)

    let api = $"%s{space.Value}.Browse.Folder"

    SynologyApi.createRequest address $"webapi/entry.cgi/%s{api}"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues api 1 "get" (Some sid)
       @ [ folderParam ]

let internal createCopyPhotoRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (space: Space)
    (photoIds: int seq)
    (targetFolderId: int)
    : HttpRequestMessage =
    let api = $"%s{space.Value}.BackgroundTask.File"
    let itemIds = Seq.map string photoIds |> Seq.reduce (fun x y -> $"%s{x},%s{y}")

    SynologyApi.createRequest address $"webapi/entry.cgi/%s{api}"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues api 1 "copy" (Some sid)
       @ [ ("target_folder_id", string targetFolderId)
           ("item_id", $"[%s{itemIds}]")
           ("action", "skip")
           ("folder_id", "[]") ]

type public TaskInfoDto =
    { Task_info: {| Completion: int
                    Id: int
                    Status: string
                    Total: int |} }
