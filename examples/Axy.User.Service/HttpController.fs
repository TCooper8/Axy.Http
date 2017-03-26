namespace Axy.Users

open System
open System.IO

open Axy
open Axy.Users.Types

open Newtonsoft.Json

module HttpController =
  let (|Guid|_|) str =
    match Guid.TryParse(str) with
    | false, _ -> None
    | true, guid -> Some guid

  let handleReply = function
    | NotFound ->
      Http.Response.jsonContent
      >> Http.Response.notFound (fun output ->
        use writer = new StreamWriter(output)
        writer.Write("""
          { "reason" = "Not found" }
        """)
      )

    | UserReply user ->
      user
      |> JsonConvert.SerializeObject
      |> fun payload ->
        Http.Response.jsonContent
        >> Http.Response.ok (fun output ->
          use writer = new StreamWriter(output)
          writer.Write(payload)
        )

    | UserSeq users ->
      Http.Response.jsonContent
      >> Http.Response.ok (fun output ->
        use writer = new StreamWriter(output)
        writer.Write("[\n")

        users
        |> Seq.iter (fun user ->
          user
          |> JsonConvert.SerializeObject
          |> fun payload ->
            writer.Write(payload)
            writer.Write(",\n")
        )

        writer.Write("]")
      )

    | Unavailable reason ->
      Http.Response.jsonContent
      >> Http.Response.serviceUnavailable (fun output ->
        use writer = new StreamWriter(output)
        let format = sprintf """
          { "reason" = %A
          }
        """

        writer.Write(format reason)
      )

    | ReadError e ->
      Http.Response.jsonContent
      >> Http.Response.internalServerError (fun output ->
        use writer = new StreamWriter(output)
        let format = sprintf """
          { "error": %s
          }
        """
        let payload =
          JsonConvert.SerializeObject(e)
          |> format

        writer.Write(payload)
      )

  let writeStr (str:string) (stream:Stream) =
    use writer = new StreamWriter(stream)
    writer.Write(str)

  let handleWriteReply = function
    | Ok status ->
      Http.Response.jsonContent
      >> Http.Response.ok (
        status
        |> sprintf """{ "status": %A }"""
        |> writeStr
      )

    | Created id ->
      Http.Response.jsonContent
      >> Http.Response.created (
        string id
        |> sprintf """{ "id": %A }"""
        |> writeStr
      )

    | Conflict reason ->
      Http.Response.jsonContent
      >> Http.Response.conflict (
        reason
        |> sprintf """{ "reason": %A }"""
        |> writeStr
      )

    | WriteError e ->
      Http.Response.jsonContent
      >> Http.Response.internalServerError (
        e
        |> JsonConvert.SerializeObject
        |> sprintf """{ "error": %s }"""
        |> writeStr
      )

  let getUser (state:Services.State) userId =
    state.reads.PostAndReply(fun chan -> Actor.Notify (GetUser userId, chan))
    |> handleReply

  let listUsers (state:Services.State) userId =
    state.reads.PostAndReply(fun chan -> Actor.Notify (ListUsers, chan))
    |> handleReply

  let postUser (state:Services.State) req =
    let userData = Http.Request.asString req
    ( try JsonConvert.DeserializeObject<PostUser>(userData) |> Try.success
      with e -> Try.failure e
    )
    |> (function
      | Failure e -> WriteError e
      | Success user ->
        state.writes.PostAndReply(fun chan -> Actor.Notify (WriteUser user, chan))
    )
    |> handleWriteReply

  let postUserSeq (state:Services.State) req =
    printfn "Got user seq post"
    let users = seq {
      // Read newline separated json from here.
      use input = Http.Request.mapReq (fun req -> req.InputStream) req
      use reader = new StreamReader(input)

      while not reader.EndOfStream do
        let line = reader.ReadLine()
        let user = JsonConvert.DeserializeObject<PostUser>(line)
        yield user
    }

    state.writes.PostAndReply(fun chan -> Actor.Notify (WriteUserSeq users, chan))
    |> handleWriteReply

  let users (state:Services.State) =
    Http.Controller.init "/users" []
    |> (Http.Rest.get
      <| "/users/([A-Za-z0-9_-]+)$"
      <| (fun actionParams req resp ->
        match actionParams with
        | [ Guid userId ] -> getUser state userId resp
        | [ userId ] ->
          Http.Response.badRequest (fun output ->
            use writer = new StreamWriter(output)
            writer.Write(sprintf "Invalid userId, expected valid Guid but got %s" userId)
            () 
          )
          <| resp
      )
    )
    |> (Http.Rest.post
      <| "/users"
      <| fun actionParams -> postUser state
    )
    |> (Http.Rest.post
      <| "/users/bulk"
      <| fun _ -> postUserSeq state
    )
    |> (Http.Rest.get
      <| "/users"
      <| fun _ -> listUsers state 
    )

  let init state =
    [ users state
    ]