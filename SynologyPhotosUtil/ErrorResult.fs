namespace SynologyPhotosUtil

open System
open System.Net

type ErrorResult =
    | InvalidArguments
    | InvalidUrl of InvalidUrl: string
    | RequestFailed of Exception: Exception
    | InvalidHttpResponse of StatusCode: HttpStatusCode * ReasonPhrase: string
    | InvalidApiResponse of RequestType: string * Code: int
    | AlbumNotFound of AlbumName: string
    | UnexpectedAlbumType of AlbumType: string
    | FolderNotFound of FolderPath: string
