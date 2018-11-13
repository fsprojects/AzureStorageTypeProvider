open Expecto

[<EntryPoint>]
let main args =
    let config = { defaultConfig with verbosity = Logging.LogLevel.Info; ``parallel`` = false }
    Expecto.Tests.runTestsInAssembly config args