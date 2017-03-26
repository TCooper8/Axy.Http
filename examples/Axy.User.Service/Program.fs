open System
open System.IO
open System.Net
open System.Text
open System.Text.RegularExpressions
open System.Threading

open Axy

[<EntryPoint>]
let main argv = 
  use listener = Http.Listener.listen "http://localhost:8080/"

  let state = Users.Services.init ()
  let controllers = Users.HttpController.init state

  let respond = Http.Rest.init controllers
  let events = Http.Listener.onReq listener

  events.Publish.Add (Http.Listener.defaultHandle respond)

  printfn "Press any key to exit..."
  Console.Read() |> ignore

  0