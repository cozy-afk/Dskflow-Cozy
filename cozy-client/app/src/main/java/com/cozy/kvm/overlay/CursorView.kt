package com.cozy.kvm.overlay

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.view.View

/**
 * A small arrow cursor drawn into an accessibility overlay window.
 * The window's top-left is positioned at the pointer location, so the
 * arrow's hotspot is the view origin (0,0).
 */
class CursorView(context: Context) : View(context) {

    private val fill = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        style = Paint.Style.FILL
    }
    private val stroke = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.BLACK
        style = Paint.Style.STROKE
        strokeWidth = 2f
    }

    private val arrow = Path()

    init {
        val s = context.resources.displayMetrics.density
        // Classic arrow pointing up-left, hotspot at (0,0).
        arrow.apply {
            moveTo(0f, 0f)
            lineTo(0f, 18f * s)
            lineTo(5f * s, 13f * s)
            lineTo(9f * s, 21f * s)
            lineTo(12f * s, 19f * s)
            lineTo(8f * s, 11f * s)
            lineTo(15f * s, 11f * s)
            close()
        }
    }

    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val size = (24 * resources.displayMetrics.density).toInt()
        setMeasuredDimension(size, size)
    }

    override fun onDraw(canvas: Canvas) {
        canvas.drawPath(arrow, fill)
        canvas.drawPath(arrow, stroke)
    }
}
