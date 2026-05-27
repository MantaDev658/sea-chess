import { describe, test, expect } from 'bun:test'
import { Chess } from 'chess.js'

describe('Chess Core Engine Tests', () => {
  test('should initialize with standard FEN layout', () => {
    const game = new Chess()
    expect(game.fen()).toBe('rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1')
    expect(game.turn()).toBe('w')
    expect(game.isGameOver()).toBe(false)
  })

  test('should execute legal moves correctly', () => {
    const game = new Chess()
    
    // Execute a standard King Pawn opening (e4)
    const move = game.move('e4')
    expect(move).toBeDefined()
    expect(move.san).toBe('e4')
    expect(game.fen()).toBe('rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1')
    expect(game.turn()).toBe('b') // Turn shifts to Black
  })

  test('should reject illegal moves', () => {
    const game = new Chess()
    
    // Try an illegal move (e.g. moving a rook through pawns on first turn)
    expect(() => game.move('Ra3')).toThrow()
  })

  test('should correctly identify legal move options for a selected piece', () => {
    const game = new Chess()
    
    // Get all legal target squares for a knight at g1
    const moves = game.moves({ square: 'g1', verbose: true })
    const targets = moves.map(m => m.to)
    
    expect(targets).toContain('f3')
    expect(targets).toContain('h3')
    expect(targets.length).toBe(2)
  })

  test('should detect check states', () => {
    const game = new Chess()
    
    // Execute a sequence that puts Black in check
    // 1. e4 e5 2. Qh5 Nc6 3. Bc4 Nf6 4. Qxf7# (Scholar's Mate Checkmate)
    game.move('e4')
    game.move('e5')
    game.move('Qh5')
    game.move('Nc6')
    game.move('Bc4')
    game.move('Nf6')
    
    // Ensure not currently check or checkmate
    expect(game.inCheck()).toBe(false)
    expect(game.isCheckmate()).toBe(false)

    // Execute checkmate move
    game.move('Qxf7#')
    
    expect(game.inCheck()).toBe(true)
    expect(game.isCheckmate()).toBe(true)
    expect(game.isGameOver()).toBe(true)
  })
})
