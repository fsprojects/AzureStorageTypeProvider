open Expecto

[<EntryPoint>]
let main args =
    let config = { defaultConfig with verbosity = Logging.LogLevel.Info }
    Expecto.Tests.runTestsInAssembly config args