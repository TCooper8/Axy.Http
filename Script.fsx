#load "src/Http.fs"
#r "packages/Axy.1.0.5/lib/net45/Axy.dll"

open System
open System.Net
open System.Text
open Axy

module HttpTest =
  let test () =
    use monitor =
      Actor.actorOf
        { initialState = 0
          onFailed = fun state e ->
            printfn "Error: %A" e
            Actor.Running state
          receive = fun i -> function
            | Actor.Notify msg ->
              printfn "Handled: %i" i
              Actor.Running (i + 1)
            | msg ->
              printfn "Received: %A" msg
              Actor.Running i
        }

    use handler =
      Actor.actorOf
        { initialState = ()
          onFailed = fun state e ->
            printfn "Handler failed: %A" e
            Actor.Running state
          receive = fun () -> function
            | Actor.Notify resp ->
              resp
              |> Http.Response.jsonContent
              |> Http.Response.ok "Hello"B
              |> fun () ->
                monitor.Post (Actor.Notify "Done")
              |> Actor.Running
            | msg ->
              printfn "Received: %A" msg
              Actor.Running ()
        }
    use listener = Http.listen "http://localhost:8080/"

    let respond req resp =
      handler.Post (Actor.Notify resp)

    Http.onReq listener
    |> fun events ->
      events.Publish.Add (Http.defaultHandle respond)

    //Http.reqSeq listener
    //|> Http.defaultSeqHandler respond
    //|> fun () -> printfn "Handled chunk"
    printfn "Listening..."

    Console.Read() |> ignore

HttpTest.test()
