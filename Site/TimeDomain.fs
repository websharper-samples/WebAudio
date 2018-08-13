namespace Site

open WebSharper
open WebSharper.JavaScript
open WebSharper.JQuery
open WebSharper.UI
open WebSharper.UI.Notation
open WebSharper.UI.Html
open WebSharper.UI.Client

[<JavaScript>]
module TimeDomain =

    let context = new AudioContext ()
    let mutable sourceNode = None
    let filter = context.CreateBiquadFilter()
    let buffer = ref null

    let ToList (from : Uint8Array) =
        let rec helper n res =
            if n <= -1 then res
            else helper (n - 1) (from.Get(n)::res)

        helper (from.Length - 1) []

    let CanvasEl = Elt.canvas [attr.width "512"; attr.height "256"; attr.style "background-color: black"] []

    let DrawTimeDomain (ctx : CanvasRenderingContext2D) (array : Uint8Array) =
        let c = JQuery.Of(CanvasEl.Dom)
        let width = float <| c.Width()
        let height = float <| c.Height()

        ctx.ClearRect(0., 0., width, height)
        ToList array
        |> List.iteri (fun i a -> 
                            let value = (float a) / 256.
                            let y = height - (height * value) - 1.
                            ctx.FillStyle <- "#ffffff"
                            ctx.FillRect(float i, y, 1., 1.)
                      )

    let rFilter =
        Var.Create(BiquadFilterType.Allpass)
            .Lens id (fun _ x -> filter.Type <- x; x)

    let GenRadioButton (filter : BiquadFilterType) =
        let radio = Doc.Radio [attr.name "filters"] filter rFilter
        div [] [label [] [radio; text (string filter)]]

    let Filters =
        [
            BiquadFilterType.Allpass
            BiquadFilterType.Bandpass
            BiquadFilterType.Highpass
            BiquadFilterType.Highshelf
            BiquadFilterType.Lowpass
            BiquadFilterType.Lowshelf
            BiquadFilterType.Notch
            BiquadFilterType.Peaking
        ]

    let RadioGroup =
        Filters
        |> List.map GenRadioButton

    let Analyser () =
        let analyser = context.CreateAnalyser ()
        analyser.SmoothingTimeConstant <- 0.3
        analyser.FftSize <- 1024

        let javascriptNode = context.CreateScriptProcessor (2048, 1, 1)

        //Workaroud for a bug in Chrome which makes the GC destroy the ScriptProcessorNode if it's not in global scope
        JS.Global?sourceNode <- javascriptNode

        javascriptNode.Connect(context.Destination)
        javascriptNode.Onaudioprocess <- fun _ ->
                                            let array = new Uint8Array(int(analyser.FrequencyBinCount))
                                            analyser.GetByteTimeDomainData array
                                            let ctx = (As<CanvasElement> CanvasEl.Dom).GetContext("2d")

                                            DrawTimeDomain ctx array
                                           
        filter.Type <- BiquadFilterType.Allpass
        filter.Connect(analyser)

        sourceNode <- Some <| context.CreateBufferSource()

        analyser.Connect(javascriptNode)
        filter.Connect(context.Destination)

        sourceNode
        |> Option.iter (fun e ->
                            //only need this to be able to stop audio on other tabs in the demo
                            AudioHolder.SetCurrent e

                            e.Connect(filter)
                            e.Buffer <- !buffer
                            e.Loop <- true
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

        let ignition buff = 
            context.DecodeAudioData(buff,
                fun bff ->
                    buffer := bff
                    Analyser ()
                    rFilter := BiquadFilterType.Allpass
            )

        Doc.Concat [
            CanvasEl
            div [Attr.Style "display" "inline-block"] RadioGroup
        ]
        |> Doc.Run elem
        LoadSound "diesirae.mp3" ignition
    
    let Sample =
        Samples.Build()
            .Id("TimeDomainVisualizer")
            .FileName(__SOURCE_FILE__)
            .Keywords(["webaudio"; "timedomain"; "visualizer"; "audio"])
            .Render(Main)
            .Create()