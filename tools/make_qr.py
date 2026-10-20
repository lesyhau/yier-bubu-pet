"""Make a sharp donation QR code for the About window: assets/donate/donate-qr.png.

    pip install segno opencv-python-headless
    python tools/make_qr.py --image blurry_qr.jpg     # decode an existing QR image and redraw it crisply
    python tools/make_qr.py --data "00020101021238..." # or give the text inside the QR (e.g. a VietQR string)

It prints the decoded text so you can check it (for VietQR: bank code and account number) before using it.
"""
import argparse
import os
import sys

import segno

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "donate", "donate-qr.png")


def decode(path):
    import cv2
    img = cv2.imread(path)
    if img is None:
        sys.exit(f"Can't read {path}")
    detector = cv2.QRCodeDetector()
    # Try the image as is, then upscaled (helps with small or blurry codes).
    for scale in (1, 2, 4):
        im = img if scale == 1 else cv2.resize(img, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST)
        text, points, _ = detector.detectAndDecode(im)
        if text:
            return text
    sys.exit("No QR code found in the image.")


def main():
    ap = argparse.ArgumentParser()
    group = ap.add_mutually_exclusive_group(required=True)
    group.add_argument("--image", help="existing QR code image to decode and redraw")
    group.add_argument("--data", help="the text to encode")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()

    data = decode(args.image) if args.image else args.data
    print("QR text:", data)
    if data.startswith("000201"):
        # VietQR / EMVCo: field 38 holds the bank BIN and account number.
        print("Looks like a VietQR code. Check the bank and account number above before publishing.")

    # Medium error correction, 12 px per module, 4-module white border: sharp and easy to scan.
    qr = segno.make(data, error="m", micro=False)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    qr.save(args.out, scale=12, border=4, dark="#000000", light="#FFFFFF")
    if args.image and decode(args.out) != data:
        sys.exit("The redrawn QR code doesn't decode to the same text; not using it.")
    print("Wrote", os.path.normpath(args.out))


if __name__ == "__main__":
    main()
