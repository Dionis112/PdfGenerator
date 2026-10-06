"""Independently check rendered fixture PDFs (requires pypdf)."""

import argparse
import json
import re
from pathlib import Path

from pypdf import PdfReader


def normalized(text):
    return " ".join(text.split())


def check(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path, help="TemplateValidation output directory")
    args = parser.parse_args()
    run = args.run.resolve()
    results = json.loads((run / "results.json").read_text(encoding="utf-8-sig"))
    verified = []
    for case in results["cases"]:
        pages_by_mode = {}
        for mode in case["Modes"]:
            if mode["Status"] != "pdf-created":
                continue
            path = run / case["Id"].replace("/", "-") / (mode["Mode"] + ".pdf")
            reader = PdfReader(path)
            pages = [normalized(page.extract_text()) for page in reader.pages]
            check(len(pages) == mode["Pages"], f"Page count: {path}")
            check(all(pages), f"Empty text layer: {path}")
            check("\ufffd" not in "".join(pages), f"Replacement characters: {path}")
            if case["Id"].startswith("bank-"):
                check(len(pages) == 3, f"Bank page count: {path}")
                person = "Примерова Мария Игоревна" if case["Id"] == "bank-second-client" else "Тестов Алексей Сергеевич"
                check(all(person in page for page in pages), f"Client missing: {path}")
                if case["Id"] == "bank-second-client":
                    check("Тестов Алексей Сергеевич" not in "".join(pages), f"Previous client leaked: {path}")
                    check(all("DEMO-BNK-2026-002" in page for page in pages), f"Previous document id: {path}")
                for number, page in enumerate(pages, 1):
                    check("СИНТЕТИЧЕСКИЕ ДАННЫЕ." in page and f"Страница {number}" in page, f"Footer: {path}")
                check("1 200 000,00" in pages[0] and "78 000,00" in pages[0] and "1 278 000,00" in pages[0], f"Summary totals: {path}")
                first_date, last_date = ("28.02.2026", "31.01.2027") if case["Id"] == "bank-month-end" else ("06.11.2026", "06.10.2027")
                check(f"1 {first_date} 100 000,00 12 000,00 112 000,00 1 100 000,00" in pages[1], f"First payment: {path}")
                check(f"12 {last_date} 100 000,00 1 000,00 101 000,00 0,00" in pages[1], f"Last payment: {path}")
                check("ИТОГО 1 200 000,00 78 000,00 1 278 000,00 0,00" in pages[1], f"Schedule totals: {path}")
                total = "0,00" if case["Id"] == "bank-empty-services" else "1 500,00"
                check(f"Итого отдельно выбранные услуги: {total}" in pages[2], f"Services total: {path}")
                check("Не выбранная услуга" not in pages[2], f"Unselected service: {path}")
                if case["Id"] == "bank-empty-services":
                    check("Демонстрационная услуга" not in pages[2], f"Empty services: {path}")
                check(pages[2].count("Данные клиента сверены") == 1, f"Duplicate clause: {path}")
                check(len(re.findall(r"Подпись _+ Дата _+", pages[2])) == 2, f"Signature fields: {path}")
            pages_by_mode[mode["Mode"]] = pages
        if pages_by_mode:
            check(set(pages_by_mode) == {"in-process", "worker"}, f"Incomplete mode pair: {case['Id']}")
            check(pages_by_mode["in-process"] == pages_by_mode["worker"], f"Worker text differs: {case['Id']}")
            verified.append({"id": case["Id"], "pages": len(pages_by_mode["worker"]), "matchingText": True})
    check(any(case["id"] == "bank-standard" for case in verified), "Missing banking PDF")
    (run / "pdf-qa.json").write_text(json.dumps({"verified": verified, "pdfCount": len(verified) * 2}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {len(verified) * 2} PDFs; identical page text in both modes. Bank checks passed.")


if __name__ == "__main__":
    main()
