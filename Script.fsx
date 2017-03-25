#r "packages/Axy.1.0.5/lib/net45/Axy.dll"
#load "src/Http.fs"
#load "src/Controller.fs"
#load "src/Rest.fs"

open System
open System.Net
open System.Text
open Axy

module HttpTest =
  let (|Guid|_|) str =
    match Guid.TryParse(str) with
    | false, _ -> None
    | true, guid -> Some guid

  let test () =
    let users =
      Http.Controller.init "/users" []
      |> Http.Rest.get (fun req resp ->
        match req with
        | Http.Request.PathSegments [ "users"; Guid userId ] ->
          Http.Response.ok "User"B resp
        | _ ->
          Http.Response.badRequest "Invalid userId, expected valid Guid"B resp
      )

    use listener = Http.Listener.listen "http://localhost:8080/"

    let respond =
      [ users
      ]
      |> Http.Rest.init

    Http.Listener.reqSeq listener
    |> Http.Listener.defaultSeqHandler respond
    |> fun () -> printfn "Handled chunk"
    //printfn "Listening..."

    Console.Read() |> ignore

HttpTest.test()
