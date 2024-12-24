import unittest
from sec_data.company_info import CompanyInfo

class TestCompanyInfo(unittest.TestCase):
    def setUp(self):
        self.json_path = "data/company_tickers_exchange.json"
        self.company_info = CompanyInfo(self.json_path)

    def test_get_cik_by_ticker(self):
        cik = self.company_info.get_cik_by_ticker("AAPL")
        self.assertEqual(cik, 320193)

    def test_search_by_name(self):
        result = self.company_info.search_by_name("Amazon")
        self.assertGreater(len(result), 0)

if __name__ == "__main__":
    unittest.main()
