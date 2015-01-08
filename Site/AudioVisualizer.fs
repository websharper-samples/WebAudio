namespace Site

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.JQuery
open IntelliFactory.WebSharper.Html.Client
open IntelliFactory.WebSharper.JavaScript

[<JavaScript>]
module AudioVisualizer =

    let context = new AudioContext ()
    let mutable sourceNode = None
    let filter = context.CreateBiquadFilter()
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

    let DrawSpectrum (ctx : CanvasRenderingContext2D) (array : Uint8Array) =
        ToList array
        |> List.iteri (fun i a -> 
            ctx.FillRect(float(i * 5), 325. - float(a), 3., 325.))

    let Canvas = Canvas [ Width "1000"; Height "325" ]

    let ButtonEvent biq (_ : Element) (_ : Events.MouseEvent) =
        filter.Type <- biq

    let GenRadioButton (filter : BiquadFilterType) =
        let radio = Input [ Attr.Type "radio"; Attr.Name "filters"; ]
                        |>! OnClick (ButtonEvent filter)
        Div [ radio ] -< [ Text <| string filter ]

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
                                            analyser.GetByteFrequencyData array
                                            let ctx = (As<CanvasElement> Canvas.Dom).GetContext("2d")
                                            ctx.ClearRect(0., 0., 1000., 325.)
                                            let gradient = ctx.CreateLinearGradient(0.,0.,0.,300.)
                                            MkGradient gradient
                                            ctx.FillStyle <- gradient
                                            DrawSpectrum ctx array
                                           
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
                    JQuery.Of(RadioGroup.[0].Dom).Children().First().Prop("checked", "true") |> ignore
                    JQuery.Of(elem).Append((Div [Attr.Style "display: inline-block"] -< RadioGroup).Dom) |> ignore
            )

        JQuery.Of(elem).Append(Canvas.Dom) |> ignore
        LoadSound "diesirae.mp3" ignition
    
    let Sample =
        Samples.Build()
            .Id("AudioVisualizer")
            .FileName(__SOURCE_FILE__)
            .Keywords(["webaudio"; "visualizer"; "frequency"; "spectrum"])
            .Render(Main)
            .Create()