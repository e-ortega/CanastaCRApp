class StorePrice {
  // Null for chain-level scraped prices (no specific physical location) — every chain
  // scraped so far sets one nationwide price, not a per-location price. storeName/chain
  // are always present either way: a chain's display name when storeId is null.
  final String? storeId;
  final String storeName;
  final String chain;
  final double price;
  final String currency;
  final DateTime reportedAt;
  final bool isExpired;

  const StorePrice({
    this.storeId,
    required this.storeName,
    required this.chain,
    required this.price,
    required this.currency,
    required this.reportedAt,
    required this.isExpired,
  });

  factory StorePrice.fromJson(Map<String, dynamic> j) => StorePrice(
        storeId: j['storeId'],
        storeName: j['storeName'],
        chain: j['chain'].toString(),
        price: (j['price'] as num).toDouble(),
        currency: j['currency'],
        reportedAt: DateTime.parse(j['reportedAt']),
        isExpired: j['isExpired'],
      );
}

class PriceComparison {
  final String productId;
  final String productName;
  final String? productImageUrl;
  final List<StorePrice> prices;
  final double? lowestPrice;
  final double? highestPrice;
  final double? savingsAmount;
  final double? savingsPercent;

  const PriceComparison({
    required this.productId,
    required this.productName,
    this.productImageUrl,
    required this.prices,
    this.lowestPrice,
    this.highestPrice,
    this.savingsAmount,
    this.savingsPercent,
  });

  factory PriceComparison.fromJson(Map<String, dynamic> j) => PriceComparison(
        productId: j['productId'],
        productName: j['productName'],
        productImageUrl: j['productImageUrl'],
        prices: (j['prices'] as List).map((p) => StorePrice.fromJson(p)).toList(),
        lowestPrice: (j['lowestPrice'] as num?)?.toDouble(),
        highestPrice: (j['highestPrice'] as num?)?.toDouble(),
        savingsAmount: (j['savingsAmount'] as num?)?.toDouble(),
        savingsPercent: (j['savingsPercent'] as num?)?.toDouble(),
      );
}
