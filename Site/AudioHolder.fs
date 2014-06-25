namespace Site

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Html5

[<JavaScript>]
module AudioHolder =
    let mutable currentPlaying : AudioBufferSourceNode option = None

    let StopCurrent() =
        currentPlaying
        |> Option.iter (fun e -> e.Stop())

    let SetCurrent src =
        currentPlaying <- Some src