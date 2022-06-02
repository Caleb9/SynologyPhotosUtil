module Tests

open System.Net.Http
open System.Net.Http.Json
open System.Text.RegularExpressions
open SynologyPhotosAlbumList
open Xunit
open FsUnit

let private matches absoluteUri (actualContentString: string) expectedContentParams (request: HttpRequestMessage) =
    request.Method = HttpMethod.Post
    && request.RequestUri.AbsoluteUri = absoluteUri
    (* Check if each of expected content params is in the actualContentString with appropriate separators *)
    && expectedContentParams
       |> List.forall (fun (param: string) -> Regex.IsMatch(actualContentString, $"(^{param}&)|(&{param}&)|(&{param}$)"))

let private createResponseWithJsonContent content =
    new HttpResponseMessage(Content = JsonContent.Create(content))

[<Fact>]
let ``Full sunshine scenario: valid arguments for album containing 3 photos`` () =
    (* Arrange *)
    let args =
        [| "http://ds.address/"
           "my_album"
           "my_user"
           "my_password" |]

    let inaccessibleFolderApiErrorCode = 642
    
    let sendRequestStub (request: HttpRequestMessage) =
        task {
            let! actualContentString = request.Content.ReadAsStringAsync()

            return
                match request with
                | login when
                    login
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi"
                        actualContentString
                        [ "api=SYNO.API.Auth"
                          "version=7"
                          "method=login"
                          "account=my_user"
                          "passwd=my_password" ]
                    ->
                    createResponseWithJsonContent
                        {| Success = true
                           Data = {| Sid = "fake_sid" |} |}
                | getFirstBatchOfAlbums when
                    getFirstBatchOfAlbums
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.Foto.Browse.Album"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.Foto.Browse.Album"
                          "version=2"
                          "method=list"
                          "offset=0"
                          "limit=100" ]
                    -> (* response does not contain the album we're looking for *)
                    createResponseWithJsonContent
                        {| Success = true
                           Data =
                            {| List =
                                List.replicate
                                    100
                                    {| Id = 1
                                       Name = "NOT_my_album"
                                       Passphrase = "NOT_valid_passhprase" |} |} |}
                | getSecondBatchOfAlbums when
                    getSecondBatchOfAlbums
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.Foto.Browse.Album"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.Foto.Browse.Album"
                          "version=2"
                          "method=list"
                          "offset=100"
                          "limit=100" ]
                    ->
                    createResponseWithJsonContent
                        {| Success = true
                           Data =
                            {| List =
                                [ {| Id = 42
                                     Name = "my_album"
                                     Passphrase = "fake_passphrase" |} ] |} |}
                | getPhotosInAlbum when
                    getPhotosInAlbum
                    |> matches (* first batch of photos *)
                        "http://ds.address/photo/webapi/entry.cgi"
                        actualContentString
                        [ "_sid=fake_sid"
                          "passphrase=fake_passphrase"
                          "api=SYNO.Foto.Browse.Item"
                          "version=1"
                          "method=list"
                          "offset=0"
                          "limit=100" ]
                    ->
                    createResponseWithJsonContent
                        {| Success = true
                           Data =
                            {| List =
                                [ {| Id = 1 (* photo in PRIVATE folder *)
                                     Owner_user_id = 2
                                     Folder_id = 3
                                     Filename = "photo1" |}
                                  {| Id = 4 (* photo in inaccessible folder for testing error output *)
                                     Owner_user_id = 5
                                     Folder_id = 6
                                     Filename = "photo2" |}
                                  {| Id = 7 (* photo in SHARED folder *)
                                     Owner_user_id = 8
                                     Folder_id = 9
                                     Filename = "photo3" |} ] |} |}
                | getPrivateFolderForPhoto1 when
                    getPrivateFolderForPhoto1
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.Foto.Browse.Folder"
                          "version=1"
                          "method=get"
                          "id=3" ]
                    ->
                    createResponseWithJsonContent
                        {| Success = true
                           Data =
                            {| Folder =
                                {| Id = 3
                                   Name = "/c/private/folder1" |} |} |}
                | getPrivateFolderForPhoto2 when
                    getPrivateFolderForPhoto2
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.Foto.Browse.Folder"
                          "version=1"
                          "method=get"
                          "id=6" ]
                    -> (* failing response so we'll look among shared folders *)
                    createResponseWithJsonContent
                        {| Success = false
                           Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                | getSharedFolderForPhoto2 when
                    getSharedFolderForPhoto2
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.FotoTeam.Browse.Folder"
                          "version=1"
                          "method=get"
                          "id=6" ]
                    -> (* also failing response so we can test error-output *)
                    createResponseWithJsonContent
                        {| Success = false
                           Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                | getPrivateFolderForPhoto3 when
                    getPrivateFolderForPhoto3
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.Foto.Browse.Folder"
                          "version=1"
                          "method=get"
                          "id=9" ]
                    -> (* failing response so we'll look among shared folders *)
                    createResponseWithJsonContent
                        {| Success = false
                           Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                | getSharedFolderForPhoto3 when
                    getSharedFolderForPhoto3
                    |> matches
                        "http://ds.address/photo/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
                        actualContentString
                        [ "_sid=fake_sid"
                          "api=SYNO.FotoTeam.Browse.Folder"
                          "version=1"
                          "method=get"
                          "id=9" ]
                    ->
                    createResponseWithJsonContent
                        {| Success = true
                           Data = {| Folder = {| Id = 9; Name = "/b/shared/folder2" |} |} |}
                | missingSetup ->
                    failwith
                    <| $"Unexpected request received:\n%A{missingSetup}\n"
                       + $"Content: %s{actualContentString}\nDid you set up the stub correctly?"
        }

    (* Act *)
    let actualResult = Program.listPhotosInAlbum args sendRequestStub

    (* Assert *)
    match actualResult with
    | Ok stdOut ->
        stdOut
        |> should
            equal
            (sprintf
                "%s\n%s\n%s\n"
                "/b/shared/folder2/photo3"
                "/c/private/folder1/photo1"
                "ERROR: photo2 folder inaccessible")
    | Error _ ->
        actualResult
        |> should be (ofCase <@ Result<string, string * int>.Ok @>)
