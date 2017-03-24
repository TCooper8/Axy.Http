namespace Axiom

open System
open System.Net

module Http =
  let listen prefix =
    let listener = new HttpListener ()

    listener.Prefixes.Add prefix
    listener.Start()
    listener

  let reqSeq (listener:HttpListener) = seq {
    while listener.IsListening do
      yield listener.GetContext()
  }

  let onReq (events: HttpListenerContext Event) (listener:HttpListener) =
    async {
      while listener.IsListening do
        let! ctx = listener.GetContextAsync() |> Async.AwaitTask
        events.Trigger(ctx)
    }

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

    let respond (body:byte seq) (resp:HttpListenerResponse) =
      use resp = resp
      let output = resp.OutputStream
      body
      |> Seq.iter (output.WriteByte)

    let badRequest body =
      status 404 >> respond body

    let cont = status 100 >> respond Seq.empty
    let switchingProtocols = status 101 >> respond Seq.empty

    let ok body = status 200 >> respond body
    let created body = status 201 >> respond body
    let accepted body = status 202 >> respond body
    let nonAuthoratativeInfo body = status 203 >> respond body
    let noContent = status 204 >> respond Seq.empty
    let resetContent = status 205 >> respond Seq.empty
    let partialContent body = status 206 >> respond Seq.empty
