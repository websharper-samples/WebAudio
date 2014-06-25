namespace Site

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.JQuery
open IntelliFactory.WebSharper.Html
open IntelliFactory.WebSharper.Html5
open IntelliFactory.WebSharper.JavaScript

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

    let Canvas = HTML5.Tags.Canvas [ Width "60"; Height "130"; Attr.Style "display: block;" ]

    let VolumeControl = 
        Input [ Attr.Type "range"; Attr.NewAttr "min" "1"; Attr.NewAttr "max" "100"; Attr.Value "100"; Attr.Style "width: 60px" ]

    let volume = ref 1.0

    let Analyser () =
        let analyser1 = context.CreateAnalyser ()
        analyser1.SmoothingTimeConstant <- 0.3
        analyser1.FftSize <- 1024u

        let analyser2 = context.CreateAnalyser ()
        analyser2.SmoothingTimeConstant <- 0.0
        analyser2.FftSize <- 1024u

        let gainNode = context.CreateGain ()

        let javascriptNode = context.CreateScriptProcessor (2048u, 1u, 1u)

        //Workaroud for a bug in Chrome which makes the GC destroy 
        //the ScriptProcessorNode if it's not in global scope
        JavaScript.Global?sourceNode <- javascriptNode

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


        splitter.Connect(analyser1, 0u, 0u)
        splitter.Connect(analyser2, 1u, 0u)

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

        JQuery.Of(VolumeControl.Dom).On("input", fun e ->
                                                    let v = As<float> (JQuery.Of(VolumeControl.Dom).Val())
                                                    let max = As<float> (JQuery.Of(VolumeControl.Dom).Prop("max"))
                                                    let fraction = v / max
                                                    volume := fraction * fraction
                                                    false
                                       )

        let maind = Div [
                            Canvas
                            VolumeControl
                        ]

        let ignition buff = 
            context.DecodeAudioData(buff,
                fun bff ->
                    buffer := bff
                    Analyser ()
            )

        JQuery.Of(elem).Append(maind.Dom) |> ignore
        LoadSound "diesirae.mp3" ignition

    let Sample =
        Samples.Build()
            .Id("ChannelSplitter")
            .FileName(__SOURCE_FILE__)
            .Keywords(["webaudio"; "vizualiser"; "channels"; "volume control"])
            .Render(Main)
            .Create()            
