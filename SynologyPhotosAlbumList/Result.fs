module SynologyPhotosAlbumList.Result

let bindSyncToAsync
    (binder: 'T1 -> Async<Result<'T2, 'TError>>)
    (result: Result<'T1, 'TError>)
    : Async<Result<'T2, 'TError>> =
    async {
        match result with
        | Ok t -> return! binder t
        | Error e -> return Error e
    }

let bindAsyncToAsync
    (binder: 'T1 -> Async<Result<'T2, 'TError>>)
    (result: Async<Result<'T1, 'TError>>)
    : Async<Result<'T2, 'TError>> =
    async {
        match! result with
        | Ok t -> return! binder t
        | Error e -> return Error e
    }

let bindAsyncToSync
    (binder: 'T1 -> Result<'T2, 'TError>)
    (result: Async<Result<'T1, 'TError>>)
    : Async<Result<'T2, 'TError>> =
    async {
        match! result with
        | Ok t -> return binder t
        | Error e -> return Error e
    }

let mapAsyncToAsync (mapping: 'T -> Async<'U>) (result: Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
    async {
        match! result with
        | Ok t ->
            let! mappingResult = mapping t
            return Ok mappingResult
        | Error e -> return Error e
    }

let mapAsyncToSync (mapping: 'T -> 'U) (result: Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
    async {
        match! result with
        | Ok t -> return Ok <| mapping t
        | Error e -> return Error e
    }
