class PantryItem {
  final String id;
  final String productId;
  final String productName;
  final String? productImageUrl;
  final double quantity;
  final String unit;
  final double minThreshold;
  final bool isRunningLow;
  final DateTime? lastPurchasedAt;

  const PantryItem({
    required this.id,
    required this.productId,
    required this.productName,
    this.productImageUrl,
    required this.quantity,
    required this.unit,
    required this.minThreshold,
    required this.isRunningLow,
    this.lastPurchasedAt,
  });

  factory PantryItem.fromJson(Map<String, dynamic> j) => PantryItem(
        id: j['id'],
        productId: j['productId'],
        productName: j['productName'],
        productImageUrl: j['productImageUrl'],
        quantity: (j['quantity'] as num).toDouble(),
        unit: j['unit'].toString(),
        minThreshold: (j['minThreshold'] as num).toDouble(),
        isRunningLow: j['isRunningLow'],
        lastPurchasedAt: j['lastPurchasedAt'] != null ? DateTime.parse(j['lastPurchasedAt']) : null,
      );

  double get stockPercent => minThreshold > 0 ? (quantity / minThreshold).clamp(0.0, 1.0) : 1.0;
}
