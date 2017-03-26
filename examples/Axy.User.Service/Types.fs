namespace Axy.Users

open System

module Types =
  type User = {
    id: Guid
    name: string
  }

  type PostUser = {
    name: string
  }

  type Read =
    | GetUser of userId:Guid
    | ListUsers

  type ReadReply =
    | UserReply of User
    | NotFound
    | UserSeq of User seq
    | Unavailable of reason:string
    | ReadError of exn

  type Write =
    | WriteUser of PostUser
    | WriteUserSeq of PostUser seq

  type WriteReply =
    | Ok of status:string
    | Created of id:Guid
    | Conflict of reason:string
    | WriteError of exn