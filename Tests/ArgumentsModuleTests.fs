module Tests.ArgumentsModuleTests

open System
open Xunit
open FsUnit
open SynologyPhotosAlbumList

let private assertOkCommand actualResult expectedCommand =
    match actualResult with
    | Ok command -> command |> should equal expectedCommand
    | Error _ ->
        actualResult
        |> should be (ofCase <@ Result<Arguments.Command, ErrorResult>.Ok @>)

let private assertErrorResult actualResult expectedErrorResult =
    match actualResult with
    | Error error -> error |> should equal expectedErrorResult
    | Ok _ ->
        actualResult
        |> should be (ofCase <@ Result<Arguments.Command, ErrorResult>.Error @>)

[<Fact>]
let ``Empty arguments is invalid`` () =
    Array.empty
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``Valid list command with verbose arguments`` () =
    [| "--address"
       "http://some.address"
       "--user"
       "my_user"
       "--password"
       "my_password"
       "--otp"
       "123456"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = Some(Arguments.Otp "123456") }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``Valid list command with non-verbose arguments`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "-o"
       "123456"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = Some(Arguments.Otp "123456") }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``Valid list command with mixed-verbosity arguments`` () =
    [| "-a"
       "http://some.address"
       "--user"
       "my_user"
       "-p"
       "my_password"
       "--otp"
       "123456"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = Some(Arguments.Otp "123456") }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``Valid list command when command is first`` () =
    [| "list"
       "My Album"
       "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "-o"
       "123456" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = Some(Arguments.Otp "123456") }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``Valid list command without OTP`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }
        
[<Fact>]
let ``Valid export command`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "export"
       "My Album"
       "/some/folder" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ExportAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album"
          FolderPath = Arguments.FolderPath "/some/folder" }

[<Fact>]
let ``When list command but album argument is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``When export command but arguments are missing, ErrorResult.InvalidArguments is returned`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "export" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``When export command but folder argument is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "export"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``Valid help option on it's own`` () =
    [| "--help" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.Help

[<Fact>]
let ``When help option is specified with other arguments, help takes precedence`` () =
    [| "-a"
       "http://some.address"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "-o"
       "123456"
       "list"
       "My Album"
       "--help" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.Help

[<Fact>]
let ``When list option with invalid URL, ErrorResult.InvalidUrl is returned`` () =
    [| "-a"
       "NOT A URL"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidUrl "NOT A URL"

[<Fact>]
let ``When address is HTTP without port, port 5000 is used`` () =
    [| "-a"
       "http://some.address/"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("http://some.address:5000"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``When address is HTTPS without port, port 5001 is used`` () =
    [| "-a"
       "https://some.address/"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("https://some.address:5001"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``When address is without port and contains path, path is preserved`` () =
    [| "-a"
       "https://some.address/some/path"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("https://some.address:5001/some/path"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``When address contains port, port is not changed`` () =
    [| "-a"
       "https://some.address:42/"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertOkCommand
    <| Arguments.Command.ListAlbum
        { Address = Arguments.Address(Uri("https://some.address:42"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }

[<Fact>]
let ``When address scheme is not HTTP or HTTPS, ErrorResult.InvalidUrl is returned`` () =
    [| "-a"
       "ftp://some.address/"
       "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidUrl "ftp://some.address/"

[<Fact>]
let ``When command is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "-a"
       "NOT A URL"
       "-u"
       "my_user"
       "-p"
       "my_password" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``When list command but address is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "-u"
       "my_user"
       "-p"
       "my_password"
       "list"
       "My Album" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``When list command but account is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "list"
       "My Album"
       "-a"
       "http://some.address"
       "-p"
       "my_password" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments

[<Fact>]
let ``When list command but password is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "list"
       "My Album"
       "-a"
       "http://some.address"
       "-u"
       "my_user" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments
