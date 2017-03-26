namespace Axy.Http

open System.IO
open System.Text
open System.Text.RegularExpressions

open Axy

module Rest =
  let restRoute httpMethod regex action =
    Route.init 0 httpMethod (Regex(regex, RegexOptions.Compiled)) action
    |> Controller.addRoute

  let get<'a> = restRoute "GET"
  let post<'a> = restRoute "POST"
  let put<'a> = restRoute "PUT"
  let delete<'a> = restRoute "DELETE"
  let patch<'a> = restRoute "PATCH"

  let inline internal tryPickRoute req =
    List.tryPick (fun route ->
      if Request.httpMethod req <> route.httpMethod then None
      else
        let m = route.regex.Match(Request.path req)
        if not m.Success then None
        else
          [ for group in m.Groups -> group.Value ]
          |> List.tail
          |> fun ls -> Some (route, ls)
    )

  let inline internal tryPickController req =
    let path = Request.path req

    List.tryPick (fun controller ->
      if Controller.matchesPath path controller then
        tryPickRoute req controller.routes
      else None
    )

  let inline internal useRoute req resp (route, routeParams) =
    try
      route.action routeParams req resp
    with
    | :? System.IO.IOException as e ->
      raise e
    | e ->
      Response.internalServerError (fun output ->
        use writer = new StreamWriter(output)
        writer.Write(e.Message)
        ()
      )
      <| resp
      ()

  let init controllers =
    let respond req resp =
      let path = Request.path req

      tryPickController req controllers
      |> Option.map (useRoute req resp)
      |> Option.getOrElse (fun () ->
        Response.notFound (fun output ->
          use writer = new StreamWriter(output)
          writer.Write(sprintf "Path %s is not found" path)
        )
        <| resp
      )

    respond