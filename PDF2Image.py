from pdf2image import convert_from_path
import sys
import os

def pdf_to_image(pdf_path, output_path=None, dpi=300):
    if not os.path.isfile(pdf_path):
        raise FileNotFoundError(f"No such file: {pdf_path}")
    
    images = convert_from_path(pdf_path, dpi=dpi)
    
    if len(images) != 1:
        raise ValueError("The PDF must be a single page.")
    
    image = images[0]

    # Set default output path if not provided
    if output_path is None:
        base = os.path.splitext(pdf_path)[0]
        output_path = base + ".png"
    
    image.save(output_path, 'PNG')
    print(f"Saved image to {output_path}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python pdf_to_image.py <pdf_path> [output_image_path]")
        sys.exit(1)

    pdf_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) > 2 else None

    pdf_to_image(pdf_path, output_path)
