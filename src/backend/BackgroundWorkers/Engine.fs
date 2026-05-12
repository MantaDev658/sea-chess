namespace ChessPlatform.BackgroundWorkers

open ChessPlatform.Domain

module Engine =
    /// Represents an abstract interface to a chess engine (e.g., Stockfish, Random)
    type IComputerEngine =
        abstract member GetBestMove: state: GameState -> Async<MoveRequest option>

    /// A fallback engine that plays random legal moves (used temporarily instead of Stockfish).
    type RandomEngine() =
        interface IComputerEngine with
            member _.GetBestMove(state: GameState) = async {
                let legalMoves = Game.allLegalMoves state
                if List.isEmpty legalMoves then
                    return None
                else
                    let rnd = System.Random()
                    let move = legalMoves.[rnd.Next(legalMoves.Length)]
                    
                    let fromPos, toPos = Game.moveFromTo move
                    let promo =
                        match move with
                        | Promotion (_, _, pt) -> Some pt
                        | PromotionCapture (_, _, _, pt) -> Some pt
                        | _ -> None
                        
                    return Some {
                        From = fromPos
                        To = toPos
                        Promotion = promo
                    }
            }
