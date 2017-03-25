namespace Axy.Http

open System.Text

open Axy

module Rest =
  let get action (controller:'a Controller) =
    let rootPath = controller.rootPath

    Route.init 0
    <| (function
      | Http.Request.Get (Http.Request.PathSegments [ root; _ ]) when root = rootPath -> true
      | _ -> false
    )
    <| action
    |> Controller.addRoute
    <| controller

  let init controllers =
    let respond req resp =
      let path = Request.path req

      controllers
      |> List.tryPick (fun controller ->
        if Controller.matchesPath path controller then
          controller.routes
          |> List.tryFind (fun route ->
            route.predicate req
          )
        else
          None
      )
      |> Option.map (fun route ->
        Try.success ()
        |> Try.map (fun () ->
          route.action req resp
        )
        |> Try.recover (function
          | :? System.IO.IOException as e ->
            // This could be an error with the connection.
            Failure e
          | e ->
            Response.internalServerError
            <| Encoding.UTF8.GetBytes e.Message
            <| resp
            |> Success
        )
      )
      |> Option.getOrElse (fun () ->
        Response.notFound
        <| Encoding.UTF8.GetBytes (sprintf "Path %s is not found" path)
        <| resp
        |> Success
      )
      |> (function
        | Failure e ->
          printfn "UnhandledError: %A" e
        | _ -> ()
      )

    respond