import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '../../lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center font-body text-sm font-semibold tracking-wider uppercase rounded-full transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-btc-orange focus-visible:ring-offset-2 focus-visible:ring-offset-void disabled:pointer-events-none disabled:opacity-50 active:scale-95 cursor-pointer',
  {
    variants: {
      variant: {
        primary:
          'bg-gradient-to-r from-burnt-orange to-btc-orange text-white shadow-[0_0_20px_-5px_rgba(234,88,12,0.5)] hover:scale-105 hover:shadow-[0_0_30px_-5px_rgba(247,147,26,0.7)]',
        outline:
          'border-2 border-pure-light/20 text-white bg-transparent hover:border-pure-light hover:bg-pure-light/10',
        ghost:
          'text-white bg-transparent hover:bg-pure-light/10 hover:text-btc-orange',
        link:
          'text-btc-orange underline-offset-4 hover:underline lowercase tracking-normal font-mono normal-case',
      },
      size: {
        default: 'h-11 px-6 py-2.5',
        sm: 'h-9 px-4 py-1.5 text-xs',
        lg: 'h-14 px-8 py-3.5 text-base',
        icon: 'h-11 w-11 p-0 rounded-full',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'default',
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, ...props }, ref) => {
    return (
      <button
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    )
  }
)
Button.displayName = 'Button'

// eslint-disable-next-line react-refresh/only-export-components
export { Button, buttonVariants }
