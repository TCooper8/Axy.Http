namespace Axy.Http

open System
open System.IO
open System.Net

module Listener =
  let listen prefix =
    let listener = new HttpListener ()

    listener.Prefixes.Add prefix
    listener.Start()
    listener

  let reqSeq (listener:HttpListener) = seq {
    while listener.IsListening do
      yield listener.GetContext()
  }

  let onReq (listener:HttpListener) =
    let events = new Event<HttpListenerContext> ()

    async {
      while listener.IsListening do
        let! ctx = listener.GetContextAsync() |> Async.AwaitTask
        events.Trigger(ctx)
    }
    |> Async.Start

    events

  let defaultHandle (respond:HttpListenerRequest -> HttpListenerResponse -> unit) (ctx:HttpListenerContext) =
    try
      let req = ctx.Request
      let resp = ctx.Response
      do respond req resp
    with
    | :? System.IO.IOException as e ->
      match e.HResult with
      | -2146232800 ->
        () // This means the socket was shutdown.
      | _ ->
        printfn "Unhandled error: %A" e
        exit 1
    | e ->
      printfn "Error: %A" e

  let defaultSeqHandler respond (requests: HttpListenerContext seq) =
    let watch = System.Diagnostics.Stopwatch()
    watch.Start()

    requests
    |> Seq.iter (fun ctx ->
      let ti = watch.Elapsed.Ticks

      defaultHandle respond ctx

      let tf = watch.Elapsed.Ticks
      let dt = tf - ti
      printfn "Responded in %A us" (dt / 1000L)
    )

module Request =
  let inline mapReq mapping (req:HttpListenerRequest) =
    mapping req

  let path = mapReq (fun req ->
    req.Url.AbsolutePath
  )

  let httpMethod = mapReq (fun req ->
    req.HttpMethod
  )

  let (|HttpMethod|_|) httpMethod = mapReq (fun req ->
    if req.HttpMethod = httpMethod then Some req
    else None
  )
  let (|Get|_|) = (|HttpMethod|_|) "GET"
  let (|Post|_|) = (|HttpMethod|_|) "POST"
  let (|Put|_|) = (|HttpMethod|_|) "PUT"
  let (|Delete|_|) = (|HttpMethod|_|) "DELETE"
  let (|Patch|_|) = (|HttpMethod|_|) "PATCH"

  let (|Path|_|) path = mapReq (fun req ->
    if req.Url.AbsolutePath = path then Some req
    else None
  )

  let (|PathSegments|_|) = mapReq (fun req ->
    req.Url.AbsolutePath.Split('/')
    |> Array.toList
    |> List.filter (fun s -> s <> "")
    |> Some
  )

  let asString (req:HttpListenerRequest) =
    use input = req.InputStream
    use reader = new StreamReader(input)

    reader.ReadToEnd()

module Response =
  let pipe mapping (resp:HttpListenerResponse) =
    mapping resp
    resp

  let contentType value = pipe (fun resp ->
    resp.ContentType <- value
  )

  let jsonContent = contentType "application/json"

  let status value = pipe (fun resp ->
    resp.StatusCode <- value
  )

  let respond (action: Stream -> unit) (resp:HttpListenerResponse) =
    action (resp.OutputStream)

  let closeResponse = respond (fun stream ->
    use stream = stream
    ()
  )

  let cont = status 100 >> closeResponse
  let switchingProtocols = status 101 >> closeResponse

  let ok body = status 200 >> respond body
  let created body = status 201 >> respond body
  let accepted body = status 202 >> respond body
  let nonAuthoratativeInfo body = status 203 >> respond body
  let noContent = status 204 >> closeResponse
  let resetContent = status 205 >> closeResponse
  let partialContent body = status 206 >> closeResponse

  let badRequest body = status 400 >> respond body
  let unauthorized body = status 401 >> respond body
  let forbidden body = status 403 >> respond body
  let notFound body = status 404 >> respond body
  let conflict body = status 405 >> respond body

  let internalServerError body = status 500 >> respond body
  let serviceUnavailable body = status 503 >> respond body