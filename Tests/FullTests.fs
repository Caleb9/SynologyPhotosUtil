module Tests.FullTests

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open SynologyPhotosUtil
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

let private fakeLogger =
    { new ILogger with
        member _.IsEnabled _ = false
        member _.Log(_, _, _, _, _: Func<'T, exn, string>) = ()
        member _.BeginScope _ = null }

[<Fact>]
let ``Full sunshine scenario: list owned album containing 3 photos`` () =
    async {
        (* Arrange *)
        let args =
            [| "--address"
               "http://ds.address/"
               "list"
               "my_album"
               "--user"
               "my_user"
               "--password"
               "my_password"
               "--otp"
               "123456" |]

        let inaccessibleFolderApiErrorCode = 642

        let sendRequestStub (request: HttpRequestMessage) =
            task {
                let! actualContentString = request.Content.ReadAsStringAsync()

                return
                    match request with
                    | login when
                        login
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "api=SYNO.API.Auth"
                              "version=7"
                              "method=login"
                              "account=my_user"
                              "passwd=my_password"
                              "otp_code=123456" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data = {| Sid = "fake_sid" |} |}
                    | searchAlbums when
                        searchAlbums
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Search.Search"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Search.Search"
                              "version=4"
                              "method=suggest"
                              "keyword=my_album" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| List =
                                    [ {| Id = 1
                                         Name = "NOT_my_album"
                                         Type = "shared_with_me"
                                         Passphrase = "fake-passphrase" |}
                                      {| Id = 42
                                         Name = "my_album"
                                         Type = "album"
                                         Passphrase = "" |} ] |} |}
                    | getPhotosInAlbum when
                        getPhotosInAlbum
                        |> matches (* first batch of photos *)
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "_sid=fake_sid"
                              "album_id=42"
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
                                    [ {| Id = 1
                                         Owner_user_id = 2
                                         Folder_id = 3
                                         Filename = "photo1" |}
                                      {| Id = 4
                                         Owner_user_id = 5
                                         Folder_id = 6
                                         Filename = "photo2" |}
                                      {| Id = 7
                                         Owner_user_id = 8
                                         Folder_id = 9
                                         Filename = "photo3" |} ] |} |}
                    | getPersonalSpaceFolderForPhoto1 when
                        getPersonalSpaceFolderForPhoto1
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
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
                    | getPersonalSpaceFolderForPhoto2 when
                        getPersonalSpaceFolderForPhoto2
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Folder"
                              "version=1"
                              "method=get"
                              "id=6" ]
                        -> (* failing response so we'll look among shared space folders *)
                        createResponseWithJsonContent
                            {| Success = false
                               Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                    | getSharedSpaceFolderForPhoto2 when
                        getSharedSpaceFolderForPhoto2
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
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
                    | getPersonalFolderForPhoto3 when
                        getPersonalFolderForPhoto3
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Folder"
                              "version=1"
                              "method=get"
                              "id=9" ]
                        -> (* failing response so we'll look among shared space folders *)
                        createResponseWithJsonContent
                            {| Success = false
                               Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                    | getSharedSpaceFolderForPhoto3 when
                        getSharedSpaceFolderForPhoto3
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
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
        let! actualResult = Program.execute args "ExecutableName" sendRequestStub fakeLogger

        (* Assert *)
        match actualResult with
        | Ok stdOut ->
            stdOut
            |> should
                equal
                (sprintf
                    "%s\n%s\n%s\n"
                    "S: /b/shared/folder2/photo3"
                    "P: /c/private/folder1/photo1"
                    "ERROR: photo2 folder inaccessible")
        | Error _ -> actualResult |> should be (ofCase <@ Result<string, string * int>.Ok @>)
    }

[<Fact>]
let ``Full sunshine scenario: list "shared with me" album containing 3 photos`` () =
    async {
        (* Arrange *)
        let args =
            [| "--address"
               "http://ds.address/"
               "list"
               "shared_album"
               "--user"
               "my_user"
               "--password"
               "my_password"
               "--otp"
               "123456" |]

        let inaccessibleFolderApiErrorCode = 642

        let sendRequestStub (request: HttpRequestMessage) =
            task {
                let! actualContentString = request.Content.ReadAsStringAsync()

                return
                    match request with
                    | login when
                        login
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "api=SYNO.API.Auth"
                              "version=7"
                              "method=login"
                              "account=my_user"
                              "passwd=my_password"
                              "otp_code=123456" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data = {| Sid = "fake_sid" |} |}
                    | searchAlbums when
                        searchAlbums
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Search.Search"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Search.Search"
                              "version=4"
                              "method=suggest"
                              "keyword=shared_album" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| List =
                                    [ {| Id = 1
                                         Name = "shared_album"
                                         Type = "shared_with_me"
                                         Passphrase = "fake-passphrase" |}
                                      {| Id = 42
                                         Name = "another_shared_album"
                                         Type = "album"
                                         Passphrase = "" |} ] |} |}
                    | getPhotosInAlbum when
                        getPhotosInAlbum
                        |> matches (* first batch of photos *)
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Item"
                              "version=1"
                              "method=list"
                              "passphrase=fake-passphrase"
                              "offset=0"
                              "limit=100" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| List =
                                    [ {| Id = 1
                                         Owner_user_id = 2
                                         Folder_id = 3
                                         Filename = "photo1" |}
                                      {| Id = 4
                                         Owner_user_id = 5
                                         Folder_id = 6
                                         Filename = "photo2" |}
                                      {| Id = 7
                                         Owner_user_id = 8
                                         Folder_id = 9
                                         Filename = "photo3" |} ] |} |}
                    | getPersonalSpaceFolderForPhoto1 when
                        getPersonalSpaceFolderForPhoto1
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
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
                    | getPersonalSpaceFolderForPhoto2 when
                        getPersonalSpaceFolderForPhoto2
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Folder"
                              "version=1"
                              "method=get"
                              "id=6" ]
                        -> (* failing response so we'll look among shared space folders *)
                        createResponseWithJsonContent
                            {| Success = false
                               Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                    | getSharedSpaceFolderForPhoto2 when
                        getSharedSpaceFolderForPhoto2
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
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
                    | getPersonalFolderForPhoto3 when
                        getPersonalFolderForPhoto3
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Folder"
                              "version=1"
                              "method=get"
                              "id=9" ]
                        -> (* failing response so we'll look among shared space folders *)
                        createResponseWithJsonContent
                            {| Success = false
                               Error = {| Code = inaccessibleFolderApiErrorCode |} |}
                    | getSharedSpaceFolderForPhoto3 when
                        getSharedSpaceFolderForPhoto3
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.FotoTeam.Browse.Folder"
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
        let! actualResult = Program.execute args "ExecutableName" sendRequestStub fakeLogger

        (* Assert *)
        match actualResult with
        | Ok stdOut ->
            stdOut
            |> should
                equal
                (sprintf
                    "%s\n%s\n%s\n"
                    "S: /b/shared/folder2/photo3"
                    "P: /c/private/folder1/photo1"
                    "ERROR: photo2 folder inaccessible")
        | Error _ -> actualResult |> should be (ofCase <@ Result<string, string * int>.Ok @>)
    }

[<Fact>]
let ``Full sunshine scenario: export album containing 3 photos`` () =
    async {
        (* Arrange *)
        let args =
            [| "--address"
               "http://ds.address/"
               "export"
               "my_album"
               "/folder/exported_album"
               "--user"
               "my_user"
               "--password"
               "my_password"
               "--otp"
               "123456" |]

        let sendRequestStub (request: HttpRequestMessage) =
            task {
                let! actualContentString = request.Content.ReadAsStringAsync()

                return
                    match request with
                    | login when
                        login
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "api=SYNO.API.Auth"
                              "version=7"
                              "method=login"
                              "account=my_user"
                              "passwd=my_password"
                              "otp_code=123456" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data = {| Sid = "fake_sid" |} |}
                    | searchAlbums when
                        searchAlbums
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Search.Search"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Search.Search"
                              "version=4"
                              "method=suggest"
                              "keyword=my_album" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| List =
                                    [ {| Id = 1
                                         Name = "NOT_my_album"
                                         Type = "shared_with_me"
                                         Passphrase = "fake-passphrase" |}
                                      {| Id = 42
                                         Name = "my_album"
                                         Type = "album"
                                         Passphrase = "" |} ] |} |}
                    | getPhotosInAlbum when
                        getPhotosInAlbum
                        |> matches (* first batch of photos *)
                            "http://ds.address:5000/webapi/entry.cgi"
                            actualContentString
                            [ "_sid=fake_sid"
                              "album_id=42"
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
                                    [ {| Id = 1
                                         Owner_user_id = 0
                                         Folder_id = 3
                                         Filename = "photo1" |}
                                      {| Id = 4
                                         Owner_user_id = 5
                                         Folder_id = 6
                                         Filename = "photo2" |}
                                      {| Id = 7
                                         Owner_user_id = 8
                                         Folder_id = 9
                                         Filename = "photo3" |} ] |} |}
                    | getTargetFolder when
                        getTargetFolder
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.Browse.Folder"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.Browse.Folder"
                              "version=1"
                              "method=get"
                              "name=" + WebUtility.UrlEncode "/folder/exported_album" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| Folder =
                                    {| Id = 10
                                       Name = "/folder/exported_album" |} |} |}
                    | copySharedSpacePhotos when
                        copySharedSpacePhotos
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.FotoTeam.BackgroundTask.File"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.FotoTeam.BackgroundTask.File"
                              "version=1"
                              "method=copy"
                              "target_folder_id=10"
                              "item_id=" + WebUtility.UrlEncode "[1]"
                              "action=skip"
                              "folder_id=" + WebUtility.UrlEncode "[]" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| Task_info =
                                    {| Completion = 0
                                       Status = "waiting"
                                       Target_folder = {| Id = 10; Owner_user_id = 5 |}
                                       Total = 1 |} |} |}
                    | copyPersonalSpacePhotos when
                        copyPersonalSpacePhotos
                        |> matches
                            "http://ds.address:5000/webapi/entry.cgi/SYNO.Foto.BackgroundTask.File"
                            actualContentString
                            [ "_sid=fake_sid"
                              "api=SYNO.Foto.BackgroundTask.File"
                              "version=1"
                              "method=copy"
                              "item_id=" + WebUtility.UrlEncode "[7,4]"
                              "target_folder_id=10"
                              "action=skip"
                              "folder_id=" + WebUtility.UrlEncode "[]" ]
                        ->
                        createResponseWithJsonContent
                            {| Success = true
                               Data =
                                {| Task_info =
                                    {| Completion = 0
                                       Status = "waiting"
                                       Target_folder = {| Id = 10; Owner_user_id = 5 |}
                                       Total = 2 |} |} |}
                    | missingSetup ->
                        failwith
                        <| $"Unexpected request received:\n%A{missingSetup}\n"
                           + $"Content: %s{actualContentString}\nDid you set up the stub correctly?"
            }

        (* Act *)
        let! actualResult = Program.execute args "ExecutableName" sendRequestStub fakeLogger

        (* Assert *)
        match actualResult with
        | Ok stdOut ->
            stdOut
            |> should
                equal
                "Album export started. Please see Background Tasks progress in the Synology Photos web interface."
        | Error _ -> actualResult |> should be (ofCase <@ Result<string, string * int>.Ok @>)
    }

[<Fact>]
let ``Print help message`` () =
    async {
        (* Arrange *)
        let args = [| "--help" |]

        let sendRequestStub _ =
            task { return new HttpResponseMessage() }

        (* Act *)
        let! actualResult = Program.execute args "ExecutableName" sendRequestStub fakeLogger

        (* Assert *)
        match actualResult with
        | Ok stdOut ->
            stdOut
            |> should startWith "Usage: ExecutableName [options] [command] [command-arguments] [options]"
        | Error _ -> actualResult |> should be (ofCase <@ Result<string, string * int>.Ok @>)
    }
