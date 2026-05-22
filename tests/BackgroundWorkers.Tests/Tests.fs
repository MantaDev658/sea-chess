module Tests

open Expecto

let tests =
    testList "Background Worker Tests" [
        test "Dummy pass test" {
            Expect.isTrue true "Dummy check"
        }
    ]

[<EntryPoint>]
let main args =
    runTestsWithCLIArgs [] args tests
