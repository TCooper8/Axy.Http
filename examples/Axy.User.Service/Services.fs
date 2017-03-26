namespace Axy.Users

open System

open Axy
open Axy.Users.Types

open Npgsql

module Services =
  type State = {
    reads: (Read * ReadReply AsyncReplyChannel) Actor.ActorEvent Actor
    writes: (Write * WriteReply AsyncReplyChannel) Actor.ActorEvent Actor
  }

  let conn () =
    new NpgsqlConnection(
      "Server=127.0.0.1;User Id=guest;Password=guest;Database=users;"
    )

  let writeUserSeq users =
    use conn = conn ()
    conn.Open()

    use tran = conn.BeginTransaction()

    users
    |> Seq.fold (fun n user ->
      use cmd = new NpgsqlCommand("""
        insert into axy.users (
          name
        )
        values (
          :name
        );
      """, conn)
      cmd.Parameters.Add(NpgsqlParameter(":name", user.name)) |> ignore

      let res = n + int64 (cmd.ExecuteNonQuery())
      printfn "Wrote %A users" res
      res
    ) 0L
    |> fun n ->
      tran.Commit()
      Ok (sprintf "Wrote %i records" n)

  let writes () =
    let writeUser (user:PostUser) =
      use conn = conn()
      conn.Open()

      use cmd = new NpgsqlCommand("""
        insert into axy.users (
          name
        )
        values (
          :name
        )
        returning id;
      """, conn)

      cmd.Parameters.Add(NpgsqlParameter(":name", user.name)) |> ignore
      cmd.ExecuteScalar() :?> Guid
      |> Created

    let receive () = function
      | Actor.Notify (cmd, chan: WriteReply AsyncReplyChannel) ->
        try
          match cmd with
          | WriteUser user -> chan.Reply(writeUser user)
          | WriteUserSeq users -> chan.Reply(writeUserSeq users)
        with e ->
          chan.Reply(WriteError e)

        Actor.Running ()

    Actor.actorOf
      { initialState = ()

        onFailed = fun () e ->
          printfn "ReadActor failure: %A" e
          Actor.Running ()

        receive = receive
      }

  let reads () =
    let withConn action =
      use conn = conn()
      conn.Open()
      action conn

    let getUser userId = withConn (fun conn ->
      use query = new NpgsqlCommand("""
        select (
          name
        )
        from axy.users
        where id=:userId::uuid
      """, conn)

      query.Parameters.Add(NpgsqlParameter(":userId", string userId)) |> ignore

      use reader = query.ExecuteReader()
      if reader.Read () then
        let name = reader.GetString(0)
        let user =
          { id = userId
            name = name
          }
        UserReply user
      else
        NotFound
    )

    let listUsers () =
      seq {
        use conn = conn ()
        conn.Open()
        use query = new NpgsqlCommand("""
          select
            id,
            name
          from axy.users;
        """, conn)
        use reader = query.ExecuteReader()

        while reader.Read() do
          let id = reader.GetGuid(0)
          let name = reader.GetString(1)
          let user =
            { id = id
              name = name
            }
          yield user
      }
      |> UserSeq

    let receive (conn:NpgsqlConnection) = function
      | Actor.Notify (cmd, chan: ReadReply AsyncReplyChannel) ->
        try
          match cmd with
          | GetUser userId -> chan.Reply(getUser userId)
          | ListUsers -> chan.Reply(listUsers ())
        with e ->
          chan.Reply(ReadError e)

        Actor.Running conn

    Actor.actorOf
      { initialState =
          let conn = conn ()
          conn.Open()
          conn

        onFailed = fun _ e ->
          printfn "ReadActor failure: %A" e
          Actor.Running (conn())

        receive = receive
      }

  let init () =
    use conn = conn ()
    conn.Open()
    use cmd = new NpgsqlCommand("""
      create extension if not exists pgcrypto;
      create schema if not exists axy;
      create table if not exists axy.users (
        id uuid primary key default gen_random_uuid(),
        name text
      );
    """, conn)

    cmd.ExecuteNonQuery() |> ignore

    { reads = reads ()
      writes = writes ()
    }