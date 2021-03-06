namespace Site

open WebSharper
open WebSharper.JavaScript
open WebSharper.JQuery
open WebSharper.UI
open WebSharper.UI.Html
open WebSharper.UI.Client

[<JavaScript>]
module ChannelSplitter =

    let context = new AudioContext ()
    let mutable sourceNode = None
    let buffer = ref null

    let MkGradient (gradient : CanvasGradient) =
        gradient.AddColorStop(1.,"#000000");
        gradient.AddColorStop(0.75,"#ff0000");
        gradient.AddColorStop(0.25,"#ffff00");
        gradient.AddColorStop(0.,"#ffffff");

    let ToList (from : Uint8Array) =
        let rec helper n res =
            if n <= -1 then res
            else helper (n - 1) (from.Get(n)::res)

        helper (from.Length - 1) []

    let Canvas = Elt.canvas [attr.width "60"; attr.height "130"; attr.style "display: block;"] []

    let volume = ref 1.0

    let VolumeControl =
        input [
            attr.``type`` "range"; attr.min "1"; attr.max "100"; attr.value "100"; attr.style "width: 60px"
            on.input (fun el _ ->
                let v = As<float> (JQuery.Of(el).Val())
                let max = As<float> (JQuery.Of(el).Prop("max"))
                let fraction = v / max
                volume := fraction * fraction
            )
        ] []

    let Analyser () =
        let analyser1 = context.CreateAnalyser ()
        analyser1.SmoothingTimeConstant <- 0.3
        analyser1.FftSize <- 1024

        let analyser2 = context.CreateAnalyser ()
        analyser2.SmoothingTimeConstant <- 0.0
        analyser2.FftSize <- 1024

        let gainNode = context.CreateGain ()

        let javascriptNode = context.CreateScriptProcessor (2048, 1, 1)

        //Workaroud for a bug in Chrome which makes the GC destroy 
        //the ScriptProcessorNode if it's not in global scope
        JS.Global?sourceNode <- javascriptNode

        javascriptNode.Connect(context.Destination)
        javascriptNode.Onaudioprocess <- fun _ ->
            let array = new Uint8Array(int(analyser1.FrequencyBinCount))
            analyser1.GetByteFrequencyData array
            let average1 = ToList array
                            |> List.map float
                            |> List.average

            let array2 = new Uint8Array(int(analyser2.FrequencyBinCount))
            analyser2.GetByteFrequencyData array2
            let average2 = ToList array2
                            |> List.map float
                            |> List.average

            let ctx = (As<CanvasElement> Canvas.Dom).GetContext("2d")
            ctx.ClearRect(0., 0., 60., 130.)
            let gradient = ctx.CreateLinearGradient(0.,0.,0.,130.)                                                        
            MkGradient gradient
            ctx.FillStyle <- gradient
            ctx.FillRect(0., 130. - average1, 25., 130.)
            ctx.FillRect(30., 130. - average2, 25., 130.)

            gainNode.Gain.Value <- !volume

        let splitter = context.CreateChannelSplitter ()
        sourceNode <- Some <| context.CreateBufferSource()
        gainNode.Connect(splitter)


        splitter.Connect(analyser1, 0, 0)
        splitter.Connect(analyser2, 1, 0)

        analyser1.Connect(javascriptNode)
        gainNode.Connect(context.Destination)

        sourceNode
        |> Option.iter (fun e -> 
            //only need this to be able to stop audio on other tabs in the demo
            AudioHolder.SetCurrent e

            e.Connect(gainNode)
            e.Loop <- true
            e.Buffer <- !buffer
            e.Start())

    //WebSharper does not yet support XMLHttpRequest level 2 so some inline JavaScript is needed here
    [<Direct @"
        var xhr = new XMLHttpRequest();
        xhr.open('GET', $url, true);
        xhr.responseType = 'arraybuffer';
        xhr.onload = function() {
                            $callback(xhr.response);
                        };
        xhr.send();
    ">]
    let LoadSound (url : string) (callback : ArrayBuffer -> unit) = X<unit>
 
    let Stop () =
        sourceNode
        |> Option.iter (fun e -> e.Stop())

    let Main (elem : Dom.Element) =
        Stop ()
        AudioHolder.StopCurrent()

        Doc.Concat [
            Canvas
            VolumeControl
        ]
        |> Doc.Run elem

        let ignition buff = 
            context.DecodeAudioData(buff,
                fun bff ->
                    buffer := bff
                    Analyser ()
            )

        LoadSound "diesirae.mp3" ignition

    let Sample =
        Samples.Build()
            .Id("ChannelSplitter")
            .FileName(__SOURCE_FILE__)
            .Keywords(["webaudio"; "vizualiser"; "channels"; "volume control"])
            .Render(Main)
            .Create()            
