[<AutoOpen>]
module Expecto.AzureHelpers

let beforeAfter before after test () =
    try before(); test() |> ignore
    finally after()
let beforeAfterAsync before after test = async {
    before()
    try return! test
    finally after() }

let shouldEqual a b = Expect.equal b a ""
