namespace Axy.Http

open System.Net
open System.Text.RegularExpressions

type 'a Route = {
  priority: int
  httpMethod: string
  regex: Regex
  action: string list -> HttpListenerRequest -> HttpListenerResponse -> unit
}

type 'a Controller = {
  rootPath: string
  routes: 'a Route list
}

module Route =
  let init priority httpMethod regex action =
    { priority = priority
      httpMethod = httpMethod
      regex = regex
      action = action
    }

module Controller =
  let init rootPath routes =
    { rootPath = rootPath
      routes = routes
    }

  let addRoute route controller =
    { controller with
        routes =
          route::controller.routes
          |> List.sortBy (fun route ->
            route.priority
          )
    }

  let matchesPath (path:string) controller =
    path.StartsWith(controller.rootPath)