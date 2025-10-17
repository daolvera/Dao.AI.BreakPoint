module.exports = {
  "/api/*": {
    target:
      process.env["services__breakpointapi__https__0"] ||
      process.env["services__breakpointapi__http__0"] ||
      process.env["services__apiservice__https__0"] ||
      process.env["services__apiservice__http__0"],
    secure: process.env["NODE_ENV"] !== "development",
    changeOrigin: true,
    logLevel: "debug",
    pathRewrite: {
      "^/api": "" // Remove /api prefix when forwarding to the API
    }
  },
};
