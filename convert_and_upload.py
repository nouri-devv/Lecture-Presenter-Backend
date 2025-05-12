import os
import sys
import json
import uuid
import tempfile
from dotenv import load_dotenv
from pdf2image import convert_from_path
from minio import Minio
from concurrent.futures import ThreadPoolExecutor

# Load .env variables
load_dotenv()

MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY")
BUCKET_NAME = "storage"


def generate_id():
    return uuid.uuid4().hex[:8]


def upload_image(minio_client, image_path, object_name):
    with open(image_path, "rb") as f:
        minio_client.put_object(
            BUCKET_NAME,
            object_name,
            f,
            length=os.path.getsize(image_path),
            content_type="image/png"
        )


def process_page(index, image, temp_dir, session_id, minio_client):
    object_name = f"sessions/{session_id}/slides/slide_{index + 1:03}.png"
    image_path = os.path.join(temp_dir, f"slide_{index + 1}.png")
    image.save(image_path, "PNG")
    upload_image(minio_client, image_path, object_name)

    return {
        "slideNumber": index + 1,
        "slideLocation": f"{object_name}"
    }


def main(pdf_path, session_id):
    if not os.path.isfile(pdf_path):
        print(json.dumps({"error": f"PDF file not found: {pdf_path}"}))
        sys.exit(1)

    minio_client = Minio(
        MINIO_ENDPOINT,
        access_key=MINIO_ACCESS_KEY,
        secret_key=MINIO_SECRET_KEY,
        secure=False  # Set to True if using HTTPS
    )

    images = convert_from_path(pdf_path, dpi=130)
    slide_records = []

    with tempfile.TemporaryDirectory() as temp_dir:
        with ThreadPoolExecutor(max_workers=4) as executor:
            futures = [
                executor.submit(process_page, idx, img, temp_dir, session_id, minio_client)
                for idx, img in enumerate(images)
            ]

            for future in futures:
                slide_records.append(future.result())

    # Output the slide records as JSON
    print(json.dumps(slide_records))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python convert_and_upload.py <pdf_path> <session_id>")
        sys.exit(1)

    pdf_path = sys.argv[1]
    session_id = sys.argv[2]
    main(pdf_path, session_id)
