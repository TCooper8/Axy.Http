namespace Axy.Http

open System.Net

type 'a Route = {
  priority: int
  predicate: HttpListenerRequest -> bool
  action: HttpListenerRequest -> HttpListenerResponse -> unit
}

type 'a Controller = {
  rootPath: string
  routes: 'a Route list
}

module Route =
  let init priority predicate action =
    { priority = priority
      predicate = predicate
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