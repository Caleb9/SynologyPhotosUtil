module SynologyPhotosAlbumList.PhotosApi

open System.Net.Http
open SynologyPhotosAlbumList

type public AlbumDto = { Id: int; Name: string }

type public ListDto<'TItem> = { List: 'TItem list }
type internal AlbumListDto = ListDto<AlbumDto>

let internal extractDataListFromResponseDto (dto: SynologyApi.ApiResponseDto<ListDto<'a>>) : 'a list =
    let { List = dataList } =
        SynologyApi.getDataFromResponseDto dto

    dataList

let internal dataListBatchSize = 100

let internal isLastBatch batch = Seq.length batch < dataListBatchSize

let internal createGetOwnedAlbumsRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (offset: int)
    : HttpRequestMessage =
    SynologyApi.createRequest address "photo/webapi/entry.cgi/SYNO.Foto.Browse.Album"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Album" 2 "list" (Some sid)
       @ [ ("offset", $"{offset}")
           ("limit", $"{dataListBatchSize}")
           ("sort_by", "album_name")
           ("sort_direction", "asc") ]

type public PhotoDto =
    { Id: int
      Owner_user_id: int
      Folder_id: int
      Filename: string }

type internal PhotoListDto = ListDto<PhotoDto>

let internal createListPhotosBatchRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (albumId: int)
    (offset: int)
    : HttpRequestMessage =
    SynologyApi.createRequest address "photo/webapi/entry.cgi"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues "SYNO.Foto.Browse.Item" 1 "list" (Some sid)
       @ [ ("album_id", $"%i{albumId}")
           ("offset", $"%i{offset}")
           ("limit", $"{dataListBatchSize}")
           ("sort_by", "filename")
           ("sort_direction", "asc") ]

type public FolderDto = { Id: int; Name: string; Shared: bool }

let internal inaccessibleFolderErrorCode = 642

let internal createGetFolderRequest
    (address: Arguments.Address)
    (sid: SynologyApi.SessionId)
    (api: string)
    (folderId: int)
    : HttpRequestMessage =
    SynologyApi.createRequest address $"photo/webapi/entry.cgi/{api}"
    <| SynologyApi.createCommonFormUrlEncodedContentKeysAndValues api 1 "get" (Some sid)
       @ [ ("api", api)
           ("version", "1")
           ("id", $"{folderId}") ]
