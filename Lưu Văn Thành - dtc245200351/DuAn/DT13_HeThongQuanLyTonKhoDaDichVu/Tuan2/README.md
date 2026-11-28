# De tai 13: He thong quan ly ton kho da dich vu - Tuan 2

- **Sinh vien:** Luu Van Thanh
- **Ma sinh vien:** dtc245200351

## Tinh nang Tuan 2 (noi tiep Tuan 1)
- Luu tru ben vung voi EF Core + SQLite va bat WAL mode.
- Quan ly trang thai ton kho (Draft, Reserved, Committed, Cancelled) va nhat ky lich su (History).
- Ho tro Idempotency-Key va kiem soat xung dot dong thoi qua Version / ExpectedVersion.

## Cach chay
1. Chay API: `dotnet run --project Inventory.Api --urls http://localhost:5227`
2. Chay Client test: `dotnet run --project Inventory.Client`
