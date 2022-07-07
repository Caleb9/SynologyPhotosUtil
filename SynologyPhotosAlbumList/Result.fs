module SynologyPhotosAlbumList.Result

let bindSyncToAsync
    (asyncBinder: 'T1 -> Async<Result<'T2, 'TError>>)
    (asyncResult: Result<'T1, 'TError>)
    : Async<Result<'T2, 'TError>> =
    async {
        match asyncResult with
        | Ok t -> return! asyncBinder t
        | Error e -> return Error e
    }

let bindAsyncToAsync
    (asyncBinder: 'T1 -> Async<Result<'T2, 'TError>>)
    (asyncResult: Async<Result<'T1, 'TError>>)
    : Async<Result<'T2, 'TError>> =
    async {
        match! asyncResult with
        | Ok t -> return! asyncBinder t
        | Error e -> return Error e
    }

let bindAsyncToSync
    (binder: 'T1 -> Result<'T2, 'TError>)
    (asyncResult: Async<Result<'T1, 'TError>>)
    : Async<Result<'T2, 'TError>> =
    async {
        let! result = asyncResult
        return Result.bind binder result
    }

let mapAsyncToAsync
    (asyncMapping: 'T -> Async<'U>)
    (asyncResult: Async<Result<'T, 'TError>>)
    : Async<Result<'U, 'TError>> =
    async {
        match! asyncResult with
        | Ok t ->
            let! mappingResult = asyncMapping t
            return Ok mappingResult
        | Error e -> return Error e
    }

let mapAsyncToSync (mapping: 'T -> 'U) (asyncResult: Async<Result<'T, 'TError>>) : Async<Result<'U, 'TError>> =
    async {
        let! result = asyncResult
        return Result.map mapping result
    }

let mapErrorAsyncToSync (mapping: 'TError -> 'U) (asyncResult: Async<Result<'T, 'TError>>) : Async<Result<'T, 'U>> =
    async {
        let! result = asyncResult
        return Result.mapError mapping result
    }
