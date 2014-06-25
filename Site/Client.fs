namespace Site

open IntelliFactory.WebSharper

[<JavaScript>]
module Client =
    let All =
        let ( !+ ) x = Samples.Set.Singleton(x)

        Samples.Set.Create [
            !+ AudioVisualizer.Sample
            !+ ChannelSplitter.Sample
            !+ TimeDomain.Sample
        ]

    let Main = All.Show()