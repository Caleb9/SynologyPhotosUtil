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
        { Address = Arguments.Address(Uri("http://some.address"))
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
        { Address = Arguments.Address(Uri("http://some.address"))
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
        { Address = Arguments.Address(Uri("http://some.address"))
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
        { Address = Arguments.Address(Uri("http://some.address"))
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
        { Address = Arguments.Address(Uri("http://some.address"))
          Credentials =
            { Account = Arguments.Account "my_user"
              Password = Arguments.Password "my_password"
              Otp = None }
          AlbumName = Arguments.AlbumName "My Album" }

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
let ``When list command and address is missing, ErrorResult.InvalidArguments is returned`` () =
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
let ``When list command and account is missing, ErrorResult.InvalidArguments is returned`` () =
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
let ``When list command and password is missing, ErrorResult.InvalidArguments is returned`` () =
    [| "list"
       "My Album"
       "-a"
       "http://some.address"
       "-u"
       "my_user" |]
    |> Arguments.parseArgs
    |> assertErrorResult
    <| ErrorResult.InvalidArguments
