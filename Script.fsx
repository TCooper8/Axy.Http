#load "src/Http.fs"
#load "paket-files/tcooper8/axiom/Library.fsx"

open System
open System.Net
open System.Text

open Axiom

module HttpTest =
  type Data = {
    time: DateTime
    largeData: string seq
  }

  let data =
    { time = DateTime.Now
      largeData = seq {
        for i in 0 .. 100 do
          yield "Hello"
      }
    }

  let test () =
    use listener = Http.listen "http://localhost:8080/"
    printfn "Listening..."

    let respond req resp =
      resp
      |> Http.Response.jsonContent
      |> Http.Response.ok "Hello"B

    Http.reqSeq listener
    |> Http.defaultSeqHandler respond
    |> fun () -> printfn "Handled chunk"

    Console.ReadKey() |> ignore

HttpTest.test()
