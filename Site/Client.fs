namespace Site

open WebSharper

[<JavaScript>]
module Client =
    let All =
        let ( !+ ) x = Samples.Set.Singleton(x)

        Samples.Set.Create [
            !+ AudioVisualizer.Sample
            !+ ChannelSplitter.Sample
            !+ TimeDomain.Sample
        ]

    [<SPAEntryPoint>]
    let Main() =
        All.Show()