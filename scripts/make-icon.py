from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / "src" / "FromZero" / "Assets"
image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((8, 8, 248, 248), radius=54, fill="#14262f")
draw.rounded_rectangle((36, 36, 220, 220), radius=37, outline="#b9e2a7", width=14)
draw.polygon([(135, 52), (79, 137), (119, 137), (105, 204), (178, 111), (137, 111)], fill="#b9e2a7")
image.save(root / "FromZero.png")
image.save(root / "FromZero.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
