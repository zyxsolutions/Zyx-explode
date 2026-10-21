#!/usr/bin/env python3
"""Gera a marca do ZYX EXPLODE: um Z geométrico com a diagonal partida (a "explosão") e estilhaços saindo do corte.

Saída (em Assets/): logo.png (claro, para o tema escuro), logo-dark.png (escuro, para o tema claro),
logo-badge.png (selo do relatório) e app.ico (7 tamanhos). Uso:  python3 tools/make-logo.py
Requer Pillow. Para mudar a marca, ajuste as constantes abaixo e rode de novo.
"""
import math
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "Assets")

S = 1024            # tela de desenho (reduzida depois, para bordas suaves)
LIGHT = (245, 245, 245, 255)
DARK = (17, 17, 17, 255)

# Geometria do Z (coordenadas na tela de 1024)
L, R, T, B = 96, 928, 96, 928      # caixa da marca (tudo é recortado nela)
BAR = 176                           # espessura das barras de cima e de baixo
DIAG_W = 340                        # largura horizontal da faixa da diagonal
GAP = 44                            # fresta horizontal que parte a diagonal em duas
SLIDE = 46                          # quanto cada metade escorrega para o seu lado
# estilhaços lançados pela fresta: tamanhos de dentro para fora, folga até a ponta da diagonal e entre eles
SHARDS = [40, 18]
SHARD_CLEAR = 24
SHARD_STEP = 12


def poly_mask(points):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    return m


def rect_mask(x0, y0, x1, y1):
    return poly_mask([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def shift(mask, dx, dy=0):
    out = Image.new("L", (S, S), 0)
    out.paste(mask, (round(dx), round(dy)))
    return out


def both(a, b):
    return Image.composite(a, Image.new("L", (S, S), 0), b)


def union(*masks):
    out = masks[0]
    for m in masks[1:]:
        out = Image.composite(Image.new("L", (S, S), 255), out, m)
    return out


def build_mask():
    box = rect_mask(L, T, R, B)
    cx, cy = (L + R) / 2, (T + B) / 2
    y1, y2 = T + BAR, B - BAR
    run = (y2 - y1) * 0.9                      # inclinação da diagonal (~48° da horizontal)
    diag = poly_mask([(R, y1 - 2), (R - DIAG_W, y1 - 2), (R - DIAG_W - run, y2 + 2), (R - run, y2 + 2)])

    upper = both(diag, rect_mask(0, 0, S, cy - GAP / 2))
    lower = both(diag, rect_mask(0, cy + GAP / 2, S, S))
    upper = shift(upper, +SLIDE)               # a metade de cima escorrega para a direita
    lower = shift(lower, -SLIDE)               # a de baixo, para a esquerda

    top = rect_mask(L, T, R, T + BAR)
    bottom = rect_mask(L, B - BAR, R, B)
    z = both(union(top, bottom, upper, lower), box)

    # estilhaços: losangos lançados pela fresta, a partir das pontas reais das duas metades, sempre com folga
    def edge(mask, y, left):
        px = mask.load()
        xs = [x for x in range(S) if px[x, y]]
        return (min(xs) if left else max(xs)) if xs else None

    right_tip = edge(upper, int(cy - GAP / 2) - 1, left=False)     # ponta de baixo, à direita, da metade de cima
    left_tip = edge(lower, int(cy + GAP / 2) + 1, left=True)       # ponta de cima, à esquerda, da metade de baixo
    right_tip = min(right_tip, R) if right_tip else R - 120
    left_tip = max(left_tip, L) if left_tip else L + 120
    sh = Image.new("L", (S, S), 0)
    sd = ImageDraw.Draw(sh)

    def diamond(x, size):
        r = size / 2 * math.sqrt(2)
        sd.polygon([(x, cy - r), (x + r, cy), (x, cy + r), (x - r, cy)], fill=255)

    x_r = right_tip + SHARD_CLEAR
    x_l = left_tip - SHARD_CLEAR
    for size in SHARDS:
        r = size / 2 * math.sqrt(2)
        diamond(x_r + r, size)
        diamond(x_l - r, size)
        x_r += 2 * r + SHARD_STEP
        x_l -= 2 * r + SHARD_STEP
    return both(union(z, sh), box)


def tint(mask, color):
    img = Image.new("RGBA", mask.size, color[:3] + (0,))
    img.putalpha(mask)
    return img


def badge(mask, size, bg, fg, radius_frac=0.22, inset_frac=0.17):
    """Quadrado arredondado com a marca centralizada (mesmo padrão do ícone original)."""
    big = size * 4
    base = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    ImageDraw.Draw(base).rounded_rectangle([0, 0, big - 1, big - 1], radius=int(big * radius_frac), fill=bg)
    mark = tint(mask, fg).crop((L, T, R, B))          # recorta na caixa da marca
    inner = int(big * (1 - 2 * inset_frac))
    mark = mark.resize((inner, inner), Image.LANCZOS)
    off = (big - inner) // 2
    base.alpha_composite(mark, (off, off))
    return base.resize((size, size), Image.LANCZOS)


def main():
    mask = build_mask()
    crop = (L - 8, T - 8, R + 8, B + 8)

    def plain(color, size):
        img = tint(mask, color).crop(crop)
        return img.resize((size, size), Image.LANCZOS)

    plain(LIGHT, 256).save(os.path.join(ASSETS, "logo.png"))
    plain(DARK, 256).save(os.path.join(ASSETS, "logo-dark.png"))
    badge(mask, 128, LIGHT, DARK).save(os.path.join(ASSETS, "logo-badge.png"))

    frames = {n: badge(mask, n, LIGHT, DARK) for n in (16, 24, 32, 48, 64, 128, 256)}
    frames[256].save(os.path.join(ASSETS, "app.ico"), format="ICO", sizes=[(n, n) for n in frames],
                     append_images=[frames[n] for n in frames if n != 256])
    # prévia grande para conferir
    prev = os.environ.get("LOGO_PREVIEW")
    if prev:
        sheet = Image.new("RGBA", (1100, 560), (30, 30, 30, 255))
        sheet.alpha_composite(plain(LIGHT, 256), (20, 20))
        sheet.alpha_composite(badge(mask, 256, LIGHT, DARK), (300, 20))
        light_bg = Image.new("RGBA", (256, 256), (245, 245, 245, 255))
        light_bg.alpha_composite(plain(DARK, 256), (0, 0))
        sheet.alpha_composite(light_bg, (580, 20))
        x = 20
        for n in (16, 24, 32, 48, 64, 128):
            sheet.alpha_composite(frames[n], (x, 330))
            x += n + 24
        sheet.alpha_composite(badge(mask, 128, DARK, LIGHT), (x + 20, 330))
        sheet.convert("RGB").save(prev)
    print("ok")


if __name__ == "__main__":
    main()
