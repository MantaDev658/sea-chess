import React, { useState } from 'react'
import { Chess, type Square } from 'chess.js'
import { cn } from '../lib/utils'

// Synthesise retro synth beeps using Web Audio API for a futuristic mechanical/terminal feel
const playSound = (type: 'move' | 'capture' | 'check' | 'invalid') => {
  try {
    const AudioContextClass = window.AudioContext || (window as Window & typeof globalThis & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AudioContextClass) return
    const ctx = new AudioContextClass()
    
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    
    osc.connect(gain)
    gain.connect(ctx.destination)
    
    if (type === 'move') {
      osc.type = 'triangle'
      osc.frequency.setValueAtTime(440, ctx.currentTime) // A4
      osc.frequency.exponentialRampToValueAtTime(880, ctx.currentTime + 0.08) // A5
      gain.gain.setValueAtTime(0.08, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.12)
      osc.start()
      osc.stop(ctx.currentTime + 0.12)
    } else if (type === 'capture') {
      osc.type = 'sawtooth'
      osc.frequency.setValueAtTime(587.33, ctx.currentTime) // D5
      osc.frequency.exponentialRampToValueAtTime(293.66, ctx.currentTime + 0.1) // D4
      gain.gain.setValueAtTime(0.06, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15)
      osc.start()
      osc.stop(ctx.currentTime + 0.15)
    } else if (type === 'check') {
      osc.type = 'square'
      osc.frequency.setValueAtTime(987.77, ctx.currentTime) // B5
      osc.frequency.setValueAtTime(1318.51, ctx.currentTime + 0.06) // E6
      gain.gain.setValueAtTime(0.05, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.25)
      osc.start()
      osc.stop(ctx.currentTime + 0.25)
    } else if (type === 'invalid') {
      osc.type = 'sawtooth'
      osc.frequency.setValueAtTime(120, ctx.currentTime)
      osc.frequency.setValueAtTime(100, ctx.currentTime + 0.05)
      gain.gain.setValueAtTime(0.1, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15)
      osc.start()
      osc.stop(ctx.currentTime + 0.15)
    }
  } catch {
    // Audio Context is blocked or not supported
  }
}

// Custom modern vector SVG renderers for cryptographic theme chess pieces
const ChessPieceSVG: React.FC<{ type: string; color: 'w' | 'b'; className?: string }> = ({
  type,
  color,
  className,
}) => {
  const isWhite = color === 'w'
  // Digital Gold for white, Bitcoin Orange for black pieces
  const strokeColor = isWhite ? '#FFD600' : '#F7931A'
  const glowColor = isWhite ? 'rgba(255,214,0,0.4)' : 'rgba(247,147,26,0.4)'

  const svgProps = {
    viewBox: '0 0 45 45',
    className: cn('w-full h-full p-1 drop-shadow-md select-none pointer-events-none transition-all duration-300', className),
    style: {
      filter: `drop-shadow(0 0 6px ${glowColor})`,
    },
  }

  // Common styles
  const baseStroke = {
    stroke: strokeColor,
    strokeWidth: '1.5',
    strokeLinejoin: 'round' as const,
    strokeLinecap: 'round' as const,
  }

  switch (type.toLowerCase()) {
    case 'p': // Pawn
      return (
        <svg {...svgProps}>
          <path
            d="M22.5 9c-2.21 0-4 1.79-4 4 0 .89.29 1.71.78 2.38C17.33 16.5 16 18.59 16 21c0 2.03.93 3.84 2.38 5.03-.89.66-1.38 1.71-1.38 2.97h11c0-1.26-.49-2.31-1.38-2.97 1.45-1.19 2.38-3 2.38-5.03 0-2.41-1.33-4.5-3.28-5.62.49-.67.78-1.49.78-2.38 0-2.21-1.79-4-4-4z"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <path d="M12 36h21v2H12z" fill={strokeColor} {...baseStroke} />
          <path d="M15 32h15v2H15z" fill="none" {...baseStroke} />
        </svg>
      )
    case 'r': // Rook
      return (
        <svg {...svgProps}>
          <path
            d="M9 39h27v-3H9v3zm3-13h21v-4H12v4zm2.5-4l1.5-8h14l1.5 8h-17z"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <path d="M14 14h3v3h-3zm5 0h3v3h-3zm5 0h3v3h-3zm5 0h3v3h-3z" fill="none" {...baseStroke} />
          <path d="M12 32h21v3H12z" fill={strokeColor} {...baseStroke} />
        </svg>
      )
    case 'n': // Knight
      return (
        <svg {...svgProps}>
          <path
            d="M33,28.5 C33,35 29,38 22.5,38 C16,38 12,35 12,28.5 C12,23.5 15,19.5 19,17.5 C16,14.5 16,9.5 20.5,9.5 C23.5,9.5 25.5,12 25.5,15 C28.5,15.5 33,18.5 33,23.5"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <circle cx="21" cy="18" r="1.5" fill={strokeColor} />
          <path d="M11 39h23v2H11z" fill={strokeColor} {...baseStroke} />
        </svg>
      )
    case 'b': // Bishop
      return (
        <svg {...svgProps}>
          <path
            d="M9 36h27M15 32h15M22.5 8a6 6 0 0 0-6 6c0 4 6 12 6 12s6-8 6-12a6 6 0 0 0-6-6z"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <circle cx="22.5" cy="5" r="1.5" fill={strokeColor} {...baseStroke} />
          <path d="M18.5 14h8M22.5 11v6" fill="none" {...baseStroke} />
        </svg>
      )
    case 'q': // Queen
      return (
        <svg {...svgProps}>
          <path
            d="M12.5 36.5h20M9 33l3-18 6 9 4.5-12 4.5 12 6-9 3 18H9z"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <circle cx="9" cy="12" r="1.5" fill={strokeColor} />
          <circle cx="18" cy="21" r="1.5" fill={strokeColor} />
          <circle cx="22.5" cy="9" r="1.5" fill={strokeColor} />
          <circle cx="27" cy="21" r="1.5" fill={strokeColor} />
          <circle cx="36" cy="12" r="1.5" fill={strokeColor} />
        </svg>
      )
    case 'k': // King
      return (
        <svg {...svgProps}>
          <path
            d="M12.5 36.5h20M9 33l4.5-12h18L36 33H9z"
            fill={isWhite ? 'rgba(255,214,0,0.1)' : 'rgba(247,147,26,0.15)'}
            {...baseStroke}
          />
          <path d="M15 21v-4h15v4M18 13h9" fill="none" {...baseStroke} />
          <path d="M22.5 9v8M20 11.5h5" fill="none" {...baseStroke} strokeWidth="1.8" />
        </svg>
      )
    default:
      return null
  }
}

interface ChessboardProps {
  fen: string
  onMove?: (newFen: string, moveDetails: { from: string; to: string; sanitized: string }) => void
  onMoveHistory?: (moves: string[]) => void
  disabled?: boolean
}

export const Chessboard: React.FC<ChessboardProps> = ({
  fen,
  onMove,
  onMoveHistory,
  disabled = false,
}) => {
  const [game] = useState<Chess>(() => new Chess(fen))
  const [selectedSquare, setSelectedSquare] = useState<Square | null>(null)
  const [legalMoves, setLegalMoves] = useState<Square[]>([])
  const [lastMove, setLastMove] = useState<{ from: Square; to: Square } | null>(null)
  const [isFlipped, setIsFlipped] = useState(false) // Allow flipping board (White/Black perspectives)


  const files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h']
  const ranks = ['8', '7', '6', '5', '4', '3', '2', '1']

  const displayFiles = isFlipped ? [...files].reverse() : files
  const displayRanks = isFlipped ? [...ranks].reverse() : ranks

  const handleSquareClick = (square: Square) => {
    if (disabled) return

    const piece = game.get(square)

    // If a legal move target is clicked
    if (selectedSquare && legalMoves.includes(square)) {
      const from = selectedSquare
      const to = square

      try {
        const isCapture = game.get(to) !== null
        
        // Zero-latency client-side prediction
        // Execute the move immediately in the local instance
        const move = game.move({
          from,
          to,
          promotion: 'q', // Auto promote to Queen for UI simplicity
        })

        if (move) {
          setLastMove({ from, to })
          setSelectedSquare(null)
          setLegalMoves([])
          
          const isCheck = game.inCheck()
          
          // Sound chime triggers
          if (isCheck) playSound('check')
          else if (isCapture) playSound('capture')
          else playSound('move')

          // Propagate move up
          if (onMove) {
            onMove(game.fen(), {
              from,
              to,
              sanitized: move.san,
            })
          }

          if (onMoveHistory) {
            onMoveHistory(game.history())
          }
        }
      } catch {
        playSound('invalid')
      }
      return
    }

    // Selecting own piece
    if (piece && piece.color === game.turn()) {
      // Toggle selection off
      if (selectedSquare === square) {
        setSelectedSquare(null)
        setLegalMoves([])
      } else {
        setSelectedSquare(square)
        // Get all legal target squares for this piece
        const moves = game.moves({ square, verbose: true })
        setLegalMoves(moves.map((m) => m.to as Square))
      }
    } else {
      // Clicked on empty square or opponent piece (that is not a legal move target)
      setSelectedSquare(null)
      setLegalMoves([])
    }
  }

  // Get active in-check king square
  const getCheckedKingSquare = (): Square | null => {
    if (!game.inCheck()) return null
    
    // Find the king of the side whose turn it is
    const activeColor = game.turn()
    for (let r = 0; r < 8; r++) {
      for (let f = 0; f < 8; f++) {
        const fileChar = files[f]
        const rankChar = ranks[r]
        const sq = `${fileChar}${rankChar}` as Square
        const p = game.get(sq)
        if (p && p.type === 'k' && p.color === activeColor) {
          return sq
        }
      }
    }
    return null
  }

  const checkedKingSquare = getCheckedKingSquare()

  return (
    <div className="flex flex-col items-center w-full">
      {/* Interactive Cyber Chessboard Grid */}
      <div className="relative w-full max-w-[500px] aspect-square rounded-2xl overflow-hidden border border-pure-light/10 shadow-[0_0_50px_-10px_rgba(247,147,26,0.15)] bg-void">
        
        {/* Core 8x8 Grid structure */}
        <div className="grid grid-cols-8 grid-rows-8 w-full h-full">
          {displayRanks.map((rank, rIdx) =>
            displayFiles.map((file, fIdx) => {
              const squareName = `${file}${rank}` as Square
              const piece = game.get(squareName)
              const isDark = (rIdx + fIdx) % 2 === 1
              
              const isSelected = selectedSquare === squareName
              const isTarget = legalMoves.includes(squareName)
              const isCheckKing = checkedKingSquare === squareName
              
              const isLastMoveSrc = lastMove?.from === squareName
              const isLastMoveDst = lastMove?.to === squareName

              return (
                <div
                  key={squareName}
                  onClick={() => handleSquareClick(squareName)}
                  className={cn(
                    'relative flex items-center justify-center cursor-pointer transition-all duration-300 aspect-square select-none group',
                    // Background base
                    isDark ? 'bg-matter' : 'bg-[#181a20]/60',
                    // Hover squares
                    !disabled && 'hover:bg-btc-orange/10',
                    // Checked king highlight (pulsing deep red-orange)
                    isCheckKing && 'bg-red-950/80 animate-pulse border border-red-500/80 shadow-[inset_0_0_15px_rgba(239,68,68,0.5)]',
                    // Selected square glowing gold outline
                    isSelected && 'bg-gold/10 border-2 border-gold z-10 shadow-[0_0_15px_rgba(255,214,0,0.5)]',
                    // Last move highlight indicators (subtle warm orange highlight)
                    (isLastMoveSrc || isLastMoveDst) && 'bg-burnt-orange/10 border border-burnt-orange/30'
                  )}
                >
                  {/* Glowing active legal move dot overlay */}
                  {isTarget && (
                    <div className="absolute w-3.5 h-3.5 rounded-full bg-btc-orange/50 shadow-[0_0_10px_rgba(247,147,26,0.8)] z-20 group-hover:scale-125 transition-transform duration-200" />
                  )}

                  {/* Chess piece graphic */}
                  {piece && (
                    <ChessPieceSVG
                      type={piece.type}
                      color={piece.color}
                      className={cn(
                        'w-11/12 h-11/12 z-10 transition-transform duration-200',
                        !disabled && piece.color === game.turn() && 'group-hover:scale-105 cursor-grab active:cursor-grabbing'
                      )}
                    />
                  )}

                  {/* Corner Labels (Filing coordinates e.g. a1, h8) */}
                  {fIdx === 0 && (
                    <span className="absolute top-1 left-1.5 text-[8px] md:text-[9px] font-mono text-stardust/40 uppercase font-semibold">
                      {rank}
                    </span>
                  )}
                  {rIdx === 7 && (
                    <span className="absolute bottom-1 right-1.5 text-[8px] md:text-[9px] font-mono text-stardust/40 uppercase font-semibold">
                      {file}
                    </span>
                  )}
                </div>
              )
            })
          )}
        </div>
      </div>

      {/* Board Utility Toolbar */}
      <div className="flex gap-4 mt-4 font-mono">
        <button
          onClick={() => setIsFlipped(!isFlipped)}
          className="text-xs uppercase px-4 py-1.5 border border-pure-light/10 rounded-full hover:border-btc-orange/50 hover:text-btc-orange transition-all duration-300 cursor-pointer"
        >
          Flip Perspective
        </button>
        <div className="text-xs flex items-center px-4 py-1.5 border border-pure-light/10 rounded-full bg-matter text-stardust">
          TURN: <span className={cn('ml-2 font-bold uppercase', game.turn() === 'w' ? 'text-gold' : 'text-btc-orange')}>
            {game.turn() === 'w' ? 'Gold (White)' : 'Orange (Black)'}
          </span>
        </div>
      </div>
    </div>
  )
}
